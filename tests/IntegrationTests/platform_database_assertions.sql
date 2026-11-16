-- Synthetic, privileged installer fixture. Run only in an isolated disposable DB
-- inside a transaction that the runner rolls back. No real login is granted.
-- Earlier suites flush deferred checks with ALL IMMEDIATE. Restore the schema's
-- INITIALLY DEFERRED mode for this suite's multi-row atomic bootstrap fixtures.
-- The final ALL IMMEDIATE below still proves every deferred invariant.
SET CONSTRAINTS ALL DEFERRED;
CREATE ROLE bpc_platform_probe NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE bpc_reference_probe NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
GRANT USAGE ON SCHEMA bpc_security TO bpc_platform_probe,bpc_reference_probe;
GRANT EXECUTE ON FUNCTION bpc_security.issue_reference(text,text,bytea,text) TO bpc_reference_probe;
GRANT EXECUTE ON FUNCTION bpc_security.preview_owner(uuid,bytea),bpc_security.platform_bootstrap(text,text,uuid,bytea,text,uuid) TO bpc_platform_probe;
SET LOCAL ROLE bpc_reference_probe;
DO $$ DECLARE issued jsonb; i integer; BEGIN
 FOR i IN 1..5 LOOP
  issued:=bpc_security.issue_reference('https://platform-synthetic.invalid','owner',decode(repeat(i::text,64),'hex'),'Synthetic Owner');
  IF (issued->>'expiresAt')::timestamptz NOT BETWEEN clock_timestamp()+interval '23 hours 59 minutes' AND clock_timestamp()+interval '24 hours 1 minute' THEN RAISE EXCEPTION 'Reference lifetime'; END IF;
 END LOOP;
 BEGIN
  PERFORM bpc_security.issue_reference('https://platform-synthetic.invalid','owner',decode(repeat('6',64),'hex'),'Synthetic Owner');
  RAISE EXCEPTION 'Sixth issuance accepted';
 EXCEPTION WHEN SQLSTATE 'P1429' THEN NULL; END;
 BEGIN
  PERFORM bpc_security.issue_reference('https://platform-synthetic.invalid','bad',decode('00','hex'),'Synthetic');
  RAISE EXCEPTION 'Short hash accepted';
 EXCEPTION WHEN SQLSTATE 'P1004' THEN NULL; END;
END $$;
RESET ROLE;
SET LOCAL ROLE bpc_reference_probe;
SELECT bpc_security.issue_reference('https://platform-synthetic.invalid','expired',decode(repeat('a',64),'hex'),'Expired Synthetic');
SELECT bpc_security.issue_reference('https://platform-synthetic.invalid','blocked',decode(repeat('b',64),'hex'),'Blocked Synthetic');
RESET ROLE;
-- Explicit privileged fixtures are not runtime capabilities.
UPDATE identity.owner_references SET expires_at=clock_timestamp()-interval '1 second' WHERE reference_hash=decode(repeat('a',64),'hex');
UPDATE identity.identities SET status='Blocked' WHERE identity_id=(SELECT identity_id FROM identity.identity_links WHERE issuer='https://platform-synthetic.invalid' AND subject='blocked');
SELECT set_config('test.expired_owner',(SELECT identity_id::text FROM identity.identity_links WHERE issuer='https://platform-synthetic.invalid' AND subject='expired'),true);
SELECT set_config('test.blocked_owner',(SELECT identity_id::text FROM identity.identity_links WHERE issuer='https://platform-synthetic.invalid' AND subject='blocked'),true);
SET LOCAL ROLE bpc_platform_probe;
DO $$ BEGIN
 BEGIN
  PERFORM bpc_security.preview_owner(current_setting('test.expired_owner')::uuid,decode(repeat('a',64),'hex'));
  RAISE EXCEPTION 'Expired proof accepted';
 EXCEPTION WHEN SQLSTATE 'P1004' THEN NULL; END;
 BEGIN
  PERFORM bpc_security.preview_owner(current_setting('test.blocked_owner')::uuid,decode(repeat('b',64),'hex'));
  RAISE EXCEPTION 'Blocked identity accepted';
 EXCEPTION WHEN SQLSTATE 'P1004' THEN NULL; END;
END $$;
RESET ROLE;
-- Save a fixture ID in a test-only custom setting, never an authorization claim.
SELECT set_config('test.platform_owner',(SELECT identity_id::text FROM identity.identity_links WHERE issuer='https://platform-synthetic.invalid' AND subject='owner'),true);
SET LOCAL ROLE bpc_platform_probe;
DO $$ DECLARE id uuid:=current_setting('test.platform_owner')::uuid; p jsonb; first_result jsonb; replay jsonb; BEGIN
 p:=bpc_security.preview_owner(id,decode(repeat('1',64),'hex'));
 IF p<>jsonb_build_object('userId',id,'displayName','Synthetic Owner') THEN RAISE EXCEPTION 'Preview disclosure'; END IF;
 IF bpc_security.preview_owner(id,decode(repeat('1',64),'hex'))<>p THEN RAISE EXCEPTION 'Reference not reusable'; END IF;
 BEGIN
  PERFORM bpc_security.preview_owner(id,decode(repeat('0',64),'hex'));
  RAISE EXCEPTION 'Wrong proof accepted';
 EXCEPTION WHEN SQLSTATE 'P1004' THEN NULL; END;
 BEGIN
  PERFORM bpc_security.bootstrap_organization(id,'Forbidden','a0000000-0000-0000-0000-000000000001');
  RAISE EXCEPTION 'Legacy port exposed';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 first_result:=bpc_security.platform_bootstrap('https://platform-synthetic.invalid','operator',id,decode(repeat('1',64),'hex'),'Synthetic Platform Organization','a0000000-0000-0000-0000-000000000002');
 replay:=bpc_security.platform_bootstrap('https://platform-synthetic.invalid','operator',id,decode(repeat('1',64),'hex'),'Synthetic Platform Organization','a0000000-0000-0000-0000-000000000002');
 IF first_result->>'replayed'<>'false' OR replay->>'replayed'<>'true' OR first_result-'replayed'<>replay-'replayed' THEN RAISE EXCEPTION 'Replay mismatch'; END IF;
 BEGIN
  PERFORM bpc_security.platform_bootstrap('https://platform-synthetic.invalid','operator',id,decode(repeat('1',64),'hex'),'Other','a0000000-0000-0000-0000-000000000002');
  RAISE EXCEPTION 'Payload mismatch accepted';
 EXCEPTION WHEN SQLSTATE 'P1409' THEN NULL; END;
 BEGIN
  PERFORM bpc_security.preview_owner(id,decode(repeat('1',64),'hex'));
  RAISE EXCEPTION 'Consumed preview accepted';
 EXCEPTION WHEN SQLSTATE 'P1004' THEN NULL; END;
 BEGIN
  PERFORM bpc_security.platform_bootstrap('https://platform-synthetic.invalid','other-operator',id,decode(repeat('1',64),'hex'),'Synthetic Platform Organization','a0000000-0000-0000-0000-000000000002');
  RAISE EXCEPTION 'Actor crossed replay scope';
 EXCEPTION WHEN SQLSTATE 'P1004' THEN NULL; END;
 BEGIN
  EXECUTE 'SELECT * FROM identity.owner_references';
  RAISE EXCEPTION 'Direct reference access';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
RESET ROLE;
DO $$ DECLARE actor uuid; target uuid:=current_setting('test.platform_owner')::uuid; BEGIN
 SELECT identity_id INTO actor FROM identity.identity_links WHERE issuer='https://platform-synthetic.invalid' AND subject='operator';
 IF actor IS NULL OR actor=target THEN RAISE EXCEPTION 'Operator identity'; END IF;
 IF (SELECT count(*) FROM bpc_security.platform_events WHERE actor_identity_id=actor AND target_identity_id=target)<>1 THEN RAISE EXCEPTION 'Atomic operator audit'; END IF;
 IF EXISTS(SELECT 1 FROM bpc_security.events e JOIN bpc_security.platform_receipts r ON r.internal_operation_id=e.operation_id WHERE e.actor_id<>r.actor_identity_id) THEN RAISE EXCEPTION 'Tenant event falsely attributed'; END IF;
 IF (SELECT count(*) FROM identity.identity_consumptions WHERE identity_id=target)<>1 OR EXISTS(SELECT 1 FROM identity.identity_consumptions WHERE identity_id=actor) THEN RAISE EXCEPTION 'Consumption invariant'; END IF;
 IF EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.member WHERE r.rolname IN ('bpc_platform_probe','bpc_reference_probe')) THEN RAISE EXCEPTION 'Probe owner membership'; END IF;
 IF bpc_security.persistence_ready() IS DISTINCT FROM true THEN RAISE EXCEPTION 'Readiness'; END IF;
END $$;
-- Replays revalidate both identities after acquiring locks.
UPDATE identity.identities SET status='Blocked' WHERE identity_id=(SELECT identity_id FROM identity.identity_links WHERE issuer='https://platform-synthetic.invalid' AND subject='operator');
SET LOCAL ROLE bpc_platform_probe;
DO $$ BEGIN
 BEGIN
  PERFORM bpc_security.platform_bootstrap('https://platform-synthetic.invalid','operator',current_setting('test.platform_owner')::uuid,decode(repeat('1',64),'hex'),'Synthetic Platform Organization','a0000000-0000-0000-0000-000000000002');
  RAISE EXCEPTION 'Blocked actor replay accepted';
 EXCEPTION WHEN SQLSTATE 'P1004' THEN NULL; END;
END $$;
RESET ROLE;
UPDATE identity.identities SET status='Active' WHERE identity_id=(SELECT identity_id FROM identity.identity_links WHERE issuer='https://platform-synthetic.invalid' AND subject='operator');
UPDATE identity.identities SET status='Blocked' WHERE identity_id=current_setting('test.platform_owner')::uuid;
SET LOCAL ROLE bpc_platform_probe;
DO $$ BEGIN
 BEGIN
  PERFORM bpc_security.platform_bootstrap('https://platform-synthetic.invalid','operator',current_setting('test.platform_owner')::uuid,decode(repeat('1',64),'hex'),'Synthetic Platform Organization','a0000000-0000-0000-0000-000000000002');
  RAISE EXCEPTION 'Blocked target replay accepted';
 EXCEPTION WHEN SQLSTATE 'P1004' THEN NULL; END;
END $$;
RESET ROLE;
UPDATE identity.identities SET status='Active' WHERE identity_id=current_setting('test.platform_owner')::uuid;
-- Controlled audit failure must roll back the entire bootstrap, including
-- the legacy event/receipt and creation of the operator identity.
SET LOCAL ROLE bpc_reference_probe;
SELECT bpc_security.issue_reference('https://platform-synthetic.invalid','rollback-owner',decode(repeat('c',64),'hex'),'Rollback Synthetic');
RESET ROLE;
SELECT set_config('test.rollback_owner',(SELECT identity_id::text FROM identity.identity_links WHERE issuer='https://platform-synthetic.invalid' AND subject='rollback-owner'),true);
SELECT set_config('test.org_count',(SELECT count(*)::text FROM tenancy.organizations),true);
SELECT set_config('test.event_count',(SELECT count(*)::text FROM bpc_security.events),true);
SELECT set_config('test.receipt_count',(SELECT count(*)::text FROM bpc_security.operation_receipts),true);
CREATE FUNCTION bpc_security.platform_audit_failure_probe() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $$ BEGIN RAISE EXCEPTION USING ERRCODE='P1999',MESSAGE='Controlled synthetic audit failure'; END $$;
REVOKE ALL ON FUNCTION bpc_security.platform_audit_failure_probe() FROM PUBLIC;
CREATE TRIGGER platform_audit_failure_probe BEFORE INSERT ON bpc_security.platform_events FOR EACH ROW EXECUTE FUNCTION bpc_security.platform_audit_failure_probe();
SET LOCAL ROLE bpc_platform_probe;
DO $$ BEGIN
 BEGIN
  PERFORM bpc_security.platform_bootstrap('https://platform-synthetic.invalid','rollback-operator',current_setting('test.rollback_owner')::uuid,decode(repeat('c',64),'hex'),'Rollback Synthetic Organization','a0000000-0000-0000-0000-000000000003');
  RAISE EXCEPTION 'Audit failure did not abort';
 EXCEPTION WHEN SQLSTATE 'P1999' THEN NULL; END;
END $$;
RESET ROLE;
DROP TRIGGER platform_audit_failure_probe ON bpc_security.platform_events;
DROP FUNCTION bpc_security.platform_audit_failure_probe();
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM identity.identity_consumptions WHERE identity_id=current_setting('test.rollback_owner')::uuid)
 OR EXISTS(SELECT 1 FROM identity.identity_links WHERE issuer='https://platform-synthetic.invalid' AND subject='rollback-operator')
 OR EXISTS(SELECT 1 FROM bpc_security.platform_receipts WHERE operation_id='a0000000-0000-0000-0000-000000000003')
 OR (SELECT count(*) FROM tenancy.organizations)<>current_setting('test.org_count')::bigint
 OR (SELECT count(*) FROM bpc_security.events)<>current_setting('test.event_count')::bigint
 OR (SELECT count(*) FROM bpc_security.operation_receipts)<>current_setting('test.receipt_count')::bigint THEN RAISE EXCEPTION 'Audit failure left effects'; END IF;
 IF bpc_security.persistence_ready() IS DISTINCT FROM true THEN RAISE EXCEPTION 'Readiness after audit rollback'; END IF;
END $$;
-- Matching payload and a committed receipt never renew the reference TTL.
UPDATE identity.owner_references SET expires_at=clock_timestamp()-interval '1 second' WHERE reference_hash=decode(repeat('1',64),'hex');
SET LOCAL ROLE bpc_platform_probe;
DO $$ BEGIN
 BEGIN
  PERFORM bpc_security.platform_bootstrap('https://platform-synthetic.invalid','operator',current_setting('test.platform_owner')::uuid,decode(repeat('1',64),'hex'),'Synthetic Platform Organization','a0000000-0000-0000-0000-000000000002');
  RAISE EXCEPTION 'Expired matching replay accepted';
 EXCEPTION WHEN SQLSTATE 'P1004' THEN NULL; END;
END $$;
RESET ROLE;
SET CONSTRAINTS ALL IMMEDIATE;
SELECT 'Platform: issuance-limit,reusable-proof,restricted-preview,actor-scoped-replay,operator-audit,consumption,legacy-port-denied,direct-table-denied,readiness PASS';
