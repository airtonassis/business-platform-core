-- E2-04 approved scope: new objects only; no grants on existing tables or new executors.
CREATE SCHEMA sandbox AUTHORIZATION bpc_e1_ddl;
REVOKE ALL ON SCHEMA sandbox FROM PUBLIC;
SET ROLE bpc_e1_ddl;
CREATE TABLE sandbox.demo_records(
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), organization_id uuid NOT NULL REFERENCES tenancy.organizations ON DELETE RESTRICT,
 title text NOT NULL CHECK(length(title) BETWEEN 1 AND 200 AND title=btrim(title) AND title !~ '^\s*$'),
 version bigint NOT NULL DEFAULT 1 CHECK(version>0), UNIQUE(organization_id,id));
CREATE TABLE sandbox.demo_events(
 event_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), organization_id uuid NOT NULL, record_id uuid NOT NULL,
 actor_identity_id uuid NOT NULL REFERENCES identity.identities, actor_account_id uuid NOT NULL,
 operation_id uuid NOT NULL UNIQUE, kind text NOT NULL CHECK(kind IN ('DemoRecordCreated','DemoRecordUpdated')),
 resource_version bigint NOT NULL CHECK(resource_version>0), occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 FOREIGN KEY(organization_id,record_id) REFERENCES sandbox.demo_records(organization_id,id) ON DELETE RESTRICT,
 FOREIGN KEY(organization_id,actor_account_id) REFERENCES identity.enterprise_user_accounts(organization_id,account_id) ON DELETE RESTRICT,
 UNIQUE(organization_id,record_id,resource_version));
CREATE TABLE sandbox.demo_receipts(
 actor_identity_id uuid NOT NULL REFERENCES identity.identities, organization_id uuid NOT NULL,
 idempotency_key uuid NOT NULL, payload_hash bytea NOT NULL CHECK(octet_length(payload_hash)=32),
 record_id uuid NOT NULL, result jsonb NOT NULL, expires_at timestamptz NOT NULL,
 PRIMARY KEY(actor_identity_id,organization_id,idempotency_key),
 FOREIGN KEY(organization_id,record_id) REFERENCES sandbox.demo_records(organization_id,id) ON DELETE RESTRICT);
ALTER TABLE sandbox.demo_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE sandbox.demo_records FORCE ROW LEVEL SECURITY;
ALTER TABLE sandbox.demo_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE sandbox.demo_events FORCE ROW LEVEL SECURITY;
ALTER TABLE sandbox.demo_receipts ENABLE ROW LEVEL SECURITY;
ALTER TABLE sandbox.demo_receipts FORCE ROW LEVEL SECURITY;
CREATE FUNCTION sandbox.demo_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $$
BEGIN
 IF TG_TABLE_NAME<>'demo_records' OR TG_OP='DELETE' THEN RAISE EXCEPTION USING ERRCODE='P1903',MESSAGE='Immutable data'; END IF;
 IF TG_OP='UPDATE' AND (NEW.id IS DISTINCT FROM OLD.id OR NEW.organization_id IS DISTINCT FROM OLD.organization_id OR NEW.version IS DISTINCT FROM OLD.version+1) THEN
  RAISE EXCEPTION USING ERRCODE='P1903',MESSAGE='Immutable resource'; END IF;
 IF TG_OP='INSERT' AND NEW.version<>1 THEN RAISE EXCEPTION USING ERRCODE='P1903',MESSAGE='Invalid version'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER demo_immutable BEFORE INSERT OR UPDATE OR DELETE ON sandbox.demo_records FOR EACH ROW EXECUTE FUNCTION sandbox.demo_guard();
CREATE TRIGGER demo_event_immutable BEFORE UPDATE OR DELETE ON sandbox.demo_events FOR EACH ROW EXECUTE FUNCTION sandbox.demo_guard();
CREATE TRIGGER demo_receipt_immutable BEFORE UPDATE OR DELETE ON sandbox.demo_receipts FOR EACH ROW EXECUTE FUNCTION sandbox.demo_guard();
RESET ROLE;
ALTER FUNCTION sandbox.demo_guard() OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION sandbox.demo_guard() FROM PUBLIC;
GRANT USAGE ON SCHEMA sandbox TO bpc_e1_tenant_exec;
GRANT SELECT ON sandbox.demo_records TO bpc_e1_tenant_exec;
GRANT INSERT(id,organization_id,title),UPDATE(title,version) ON sandbox.demo_records TO bpc_e1_tenant_exec;
GRANT INSERT(event_id,organization_id,record_id,actor_identity_id,actor_account_id,operation_id,kind,resource_version) ON sandbox.demo_events TO bpc_e1_tenant_exec;
GRANT SELECT,INSERT ON sandbox.demo_receipts TO bpc_e1_tenant_exec;
CREATE POLICY demo_read ON sandbox.demo_records FOR SELECT TO bpc_e1_tenant_exec
 USING(organization_id=bpc_security.ctx('app.organization_id'));
CREATE POLICY demo_insert ON sandbox.demo_records FOR INSERT TO bpc_e1_tenant_exec WITH CHECK(
 organization_id=bpc_security.ctx('app.organization_id') AND version=1 AND EXISTS(SELECT FROM tenancy.memberships m JOIN identity.enterprise_user_accounts a USING(organization_id,account_id)
 WHERE m.organization_id=demo_records.organization_id AND m.account_id=bpc_security.ctx('app.account_id') AND m.status='Active' AND a.status='Active' AND m.role IN ('Owner','Admin')));
CREATE POLICY demo_update ON sandbox.demo_records FOR UPDATE TO bpc_e1_tenant_exec USING(
 organization_id=bpc_security.ctx('app.organization_id') AND EXISTS(SELECT FROM tenancy.memberships m JOIN identity.enterprise_user_accounts a USING(organization_id,account_id)
 WHERE m.organization_id=demo_records.organization_id AND m.account_id=bpc_security.ctx('app.account_id') AND m.status='Active' AND a.status='Active' AND m.role IN ('Owner','Admin')))
 WITH CHECK(organization_id=bpc_security.ctx('app.organization_id'));
CREATE POLICY demo_event_insert ON sandbox.demo_events FOR INSERT TO bpc_e1_tenant_exec WITH CHECK(
 organization_id=bpc_security.ctx('app.organization_id') AND actor_identity_id=bpc_security.ctx('app.identity_id') AND actor_account_id=bpc_security.ctx('app.account_id'));
CREATE POLICY demo_receipt ON sandbox.demo_receipts TO bpc_e1_tenant_exec USING(
 organization_id=bpc_security.ctx('app.organization_id') AND actor_identity_id=bpc_security.ctx('app.identity_id')) WITH CHECK(
 organization_id=bpc_security.ctx('app.organization_id') AND actor_identity_id=bpc_security.ctx('app.identity_id'));

-- Private authorization/lock helper. Never granted to runtime; not an authentication oracle.
CREATE FUNCTION bpc_security.demo_context(p_issuer text,p_subject text,p_org uuid,p_write boolean) RETURNS jsonb
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE ctx jsonb;
BEGIN
 ctx:=bpc_security.resolve_context(p_issuer,p_subject);
 IF ctx->>'state' IS DISTINCT FROM 'Linked' OR (ctx->>'organizationId')::uuid IS DISTINCT FROM p_org THEN
  RAISE EXCEPTION USING ERRCODE='P1904',MESSAGE='Resource unavailable'; END IF;
 IF p_write THEN
  IF ctx->>'role' IS NULL OR ctx->>'role' NOT IN ('Owner','Admin') THEN RAISE EXCEPTION USING ERRCODE='P1903',MESSAGE='Access unavailable'; END IF;
  PERFORM 1 FROM identity.identities WHERE identity_id=(ctx->>'identityId')::uuid FOR SHARE;
  PERFORM 1 FROM tenancy.organizations WHERE organization_id=p_org FOR UPDATE;
  -- All Tenant mutations serialize on org; repeat authoritative facts AFTER waiting.
  ctx:=bpc_security.resolve_context(p_issuer,p_subject);
  IF ctx->>'state' IS DISTINCT FROM 'Linked' OR (ctx->>'organizationId')::uuid IS DISTINCT FROM p_org THEN
   RAISE EXCEPTION USING ERRCODE='P1904',MESSAGE='Resource unavailable'; END IF;
  IF ctx->>'role' IS NULL OR ctx->>'role' NOT IN ('Owner','Admin') THEN RAISE EXCEPTION USING ERRCODE='P1903',MESSAGE='Access unavailable'; END IF;
 END IF;
 RETURN ctx;
END $$;
ALTER FUNCTION bpc_security.demo_context(text,text,uuid,boolean) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.demo_context(text,text,uuid,boolean) FROM PUBLIC;

CREATE FUNCTION bpc_security.read_demo_records(p_issuer text,p_subject text,p_org uuid,p_id uuid,p_limit integer,p_offset integer)
RETURNS jsonb LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE result jsonb;
BEGIN
 PERFORM bpc_security.demo_context(p_issuer,p_subject,p_org,false);
 IF p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 100 OR p_offset IS NULL OR p_offset NOT BETWEEN 0 AND 10000 THEN
  RAISE EXCEPTION USING ERRCODE='P1900',MESSAGE='Invalid page'; END IF;
 IF p_id IS NOT NULL THEN
  SELECT jsonb_build_object('id',id,'organizationId',organization_id,'title',title,'version',version) INTO result
   FROM sandbox.demo_records WHERE organization_id=p_org AND id=p_id;
  IF result IS NULL THEN RAISE EXCEPTION USING ERRCODE='P1904',MESSAGE='Resource unavailable'; END IF;
  RETURN result;
 END IF;
 SELECT jsonb_build_object('items',coalesce(jsonb_agg(value ORDER BY id) FILTER(WHERE rn<=p_limit),'[]'::jsonb),'hasMore',count(*)>p_limit) INTO result FROM (
  SELECT id,row_number() OVER(ORDER BY id) rn,jsonb_build_object('id',id,'organizationId',organization_id,'title',title,'version',version) value
  FROM (SELECT * FROM sandbox.demo_records WHERE organization_id=p_org ORDER BY id LIMIT p_limit+1 OFFSET p_offset) q) rows;
 RETURN result;
END $$;
ALTER FUNCTION bpc_security.read_demo_records(text,text,uuid,uuid,integer,integer) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.read_demo_records(text,text,uuid,uuid,integer,integer) FROM PUBLIC;

CREATE FUNCTION bpc_security.create_demo_record(p_issuer text,p_subject text,p_org uuid,p_title text,p_key uuid)
RETURNS jsonb LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE ctx jsonb; receipt sandbox.demo_receipts; item sandbox.demo_records; payload bytea; result jsonb;
BEGIN
 -- Validate a preliminary context before allocating a scoped idempotency lock.
 ctx:=bpc_security.demo_context(p_issuer,p_subject,p_org,false);
 IF ctx->>'role' IS NULL OR ctx->>'role' NOT IN ('Owner','Admin') THEN RAISE EXCEPTION USING ERRCODE='P1903',MESSAGE='Access unavailable'; END IF;
 IF p_title IS NULL OR length(p_title) NOT BETWEEN 1 AND 200 OR p_title<>btrim(p_title) OR p_title ~ '^\s*$' OR p_key IS NULL OR p_key='00000000-0000-0000-0000-000000000000' THEN
  RAISE EXCEPTION USING ERRCODE='P1900',MESSAGE='Invalid input'; END IF;
 payload:=sha256(convert_to(p_title,'UTF8'));
 PERFORM pg_advisory_xact_lock(hashtextextended('demo-create|'||(ctx->>'identityId')||'|'||p_org::text||'|'||p_key::text,19));
 ctx:=bpc_security.demo_context(p_issuer,p_subject,p_org,true);
 SELECT * INTO receipt FROM sandbox.demo_receipts WHERE actor_identity_id=(ctx->>'identityId')::uuid AND organization_id=p_org AND idempotency_key=p_key;
 IF FOUND THEN
  IF receipt.payload_hash<>payload THEN RAISE EXCEPTION USING ERRCODE='P1003',MESSAGE='Payload mismatch'; END IF;
  IF receipt.expires_at<=clock_timestamp() THEN RAISE EXCEPTION USING ERRCODE='P1909',MESSAGE='Receipt expired'; END IF;
  RETURN jsonb_build_object('item',receipt.result,'replayed',true);
 END IF;
 INSERT INTO sandbox.demo_records(id,organization_id,title) VALUES(gen_random_uuid(),p_org,p_title) RETURNING * INTO item;
 result:=jsonb_build_object('id',item.id,'organizationId',item.organization_id,'title',item.title,'version',item.version);
 INSERT INTO sandbox.demo_events(event_id,organization_id,record_id,actor_identity_id,actor_account_id,operation_id,kind,resource_version)
 VALUES(gen_random_uuid(),p_org,item.id,(ctx->>'identityId')::uuid,(ctx->>'accountId')::uuid,gen_random_uuid(),'DemoRecordCreated',1);
 INSERT INTO sandbox.demo_receipts VALUES((ctx->>'identityId')::uuid,p_org,p_key,payload,item.id,result,clock_timestamp()+interval '48 hours');
 RETURN jsonb_build_object('item',result,'replayed',false);
END $$;
ALTER FUNCTION bpc_security.create_demo_record(text,text,uuid,text,uuid) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.create_demo_record(text,text,uuid,text,uuid) FROM PUBLIC;

CREATE FUNCTION bpc_security.update_demo_record(p_issuer text,p_subject text,p_org uuid,p_id uuid,p_title text,p_version bigint)
RETURNS jsonb LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE ctx jsonb; item sandbox.demo_records;
BEGIN
 ctx:=bpc_security.demo_context(p_issuer,p_subject,p_org,true);
 SELECT * INTO item FROM sandbox.demo_records WHERE organization_id=p_org AND id=p_id FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P1904',MESSAGE='Resource unavailable'; END IF;
 IF p_version IS NULL OR item.version<>p_version THEN RAISE EXCEPTION USING ERRCODE='P1002',MESSAGE='Version conflict'; END IF;
 IF p_title IS NULL OR length(p_title) NOT BETWEEN 1 AND 200 OR p_title<>btrim(p_title) OR p_title ~ '^\s*$' THEN RAISE EXCEPTION USING ERRCODE='P1900',MESSAGE='Invalid input'; END IF;
 UPDATE sandbox.demo_records SET title=p_title,version=version+1 WHERE organization_id=p_org AND id=p_id AND version=p_version RETURNING * INTO item;
 INSERT INTO sandbox.demo_events(event_id,organization_id,record_id,actor_identity_id,actor_account_id,operation_id,kind,resource_version)
 VALUES(gen_random_uuid(),p_org,item.id,(ctx->>'identityId')::uuid,(ctx->>'accountId')::uuid,gen_random_uuid(),'DemoRecordUpdated',item.version);
 RETURN jsonb_build_object('id',item.id,'organizationId',item.organization_id,'title',item.title,'version',item.version);
END $$;
ALTER FUNCTION bpc_security.update_demo_record(text,text,uuid,uuid,text,bigint) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.update_demo_record(text,text,uuid,uuid,text,bigint) FROM PUBLIC;
-- Provisioning is NOT automatic: a separately authorized operator grants only the three public ports to Tenant runtime.

CREATE OR REPLACE FUNCTION bpc_security.persistence_ready() RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT (SELECT array_agg(version ORDER BY version)=ARRAY[1,2,3,4,5,6,7,8,9] AND bool_and(length(checksum)=64) FROM bpc_migrations.versions)
 AND (SELECT count(*)=19 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='bpc_e1_ddl') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security','sandbox') AND c.relkind='r')
 AND (SELECT count(*)=6 AND bool_and(NOT rolsuper AND NOT rolbypassrls AND NOT rolcanlogin AND NOT rolcreaterole AND NOT rolcreatedb) FROM pg_roles WHERE rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.roleid WHERE r.rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace CROSS JOIN LATERAL aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) a WHERE n.nspname IN ('bpc_security','bpc_crypto','sandbox') AND a.grantee=0 AND a.privilege_type='EXECUTE')
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='bpc_security' AND (p.prosecdef AND pg_get_userbyid(p.proowner)='bpc_e1_ddl' OR NOT coalesce(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp'],false)))
 AND to_regprocedure('bpc_security.read_tenant_audit(text,text,integer,timestamptz,uuid)') IS NULL
$$;
