-- C02 / C06 / C09. No runtime login is created or granted owner membership.
SET LOCAL ROLE bpc_e1_ddl;
CREATE TABLE identity.owner_references(reference_hash bytea PRIMARY KEY CHECK(octet_length(reference_hash)=32), identity_id uuid NOT NULL REFERENCES identity.identities, display_name text NOT NULL CHECK(length(btrim(display_name)) BETWEEN 1 AND 100), expires_at timestamptz NOT NULL);
CREATE TABLE identity.reference_emissions(identity_id uuid PRIMARY KEY REFERENCES identity.identities, emitted_at timestamptz[] NOT NULL);
CREATE TABLE bpc_security.platform_receipts(actor_identity_id uuid NOT NULL REFERENCES identity.identities, operation_id uuid NOT NULL, target_identity_id uuid NOT NULL, reference_hash bytea NOT NULL, organization_name text NOT NULL, internal_operation_id uuid NOT NULL UNIQUE, result jsonb NOT NULL, PRIMARY KEY(actor_identity_id,operation_id));
CREATE TABLE bpc_security.platform_events(event_id uuid PRIMARY KEY, actor_identity_id uuid NOT NULL REFERENCES identity.identities, target_identity_id uuid NOT NULL REFERENCES identity.identities, organization_id uuid NOT NULL REFERENCES tenancy.organizations, operation_id uuid NOT NULL, occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(), kind text NOT NULL CHECK(kind='OrganizationBootstrapped'));
RESET ROLE;
DO $$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['identity.owner_references','identity.reference_emissions','bpc_security.platform_receipts','bpc_security.platform_events'] LOOP EXECUTE format('ALTER TABLE %s ENABLE ROW LEVEL SECURITY',t); EXECUTE format('ALTER TABLE %s FORCE ROW LEVEL SECURITY',t); END LOOP; END $$;
CREATE POLICY reference_self ON identity.owner_references TO bpc_e1_identity_exec USING(identity_id=bpc_security.ctx('app.identity_id')) WITH CHECK(identity_id=bpc_security.ctx('app.identity_id'));
CREATE POLICY emission_self ON identity.reference_emissions TO bpc_e1_identity_exec USING(identity_id=bpc_security.ctx('app.identity_id')) WITH CHECK(identity_id=bpc_security.ctx('app.identity_id'));
CREATE POLICY reference_boot ON identity.owner_references TO bpc_e1_boot_exec USING(identity_id=bpc_security.ctx('app.identity_id'));
CREATE POLICY platform_receipt_scope ON bpc_security.platform_receipts TO bpc_e1_boot_exec USING(actor_identity_id=bpc_security.ctx('app.platform_actor_id')) WITH CHECK(actor_identity_id=bpc_security.ctx('app.platform_actor_id'));
CREATE POLICY platform_event_scope ON bpc_security.platform_events TO bpc_e1_boot_exec WITH CHECK(actor_identity_id=bpc_security.ctx('app.platform_actor_id'));
GRANT SELECT(reference_hash,identity_id,display_name,expires_at), INSERT(reference_hash,identity_id,display_name,expires_at) ON identity.owner_references TO bpc_e1_identity_exec;
GRANT SELECT(identity_id,emitted_at),INSERT(identity_id,emitted_at),UPDATE(emitted_at) ON identity.reference_emissions TO bpc_e1_identity_exec;
GRANT SELECT(reference_hash,identity_id,display_name,expires_at) ON identity.owner_references TO bpc_e1_boot_exec;
GRANT SELECT(actor_identity_id,operation_id,target_identity_id,reference_hash,organization_name,internal_operation_id,result),INSERT(actor_identity_id,operation_id,target_identity_id,reference_hash,organization_name,internal_operation_id,result) ON bpc_security.platform_receipts TO bpc_e1_boot_exec;
GRANT INSERT(event_id,actor_identity_id,target_identity_id,organization_id,operation_id,kind) ON bpc_security.platform_events TO bpc_e1_boot_exec;
GRANT EXECUTE ON FUNCTION bpc_security.register_identity(text,text) TO bpc_e1_boot_exec;
CREATE FUNCTION bpc_security.issue_reference(p_issuer text,p_subject text,p_hash bytea,p_display_name text) RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE id uuid; st text; times timestamptz[]; now_at timestamptz:=clock_timestamp(); expires timestamptz;
BEGIN
 IF p_hash IS NULL OR octet_length(p_hash)<>32 OR p_display_name IS NULL OR length(btrim(p_display_name)) NOT BETWEEN 1 AND 100 THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Reference unavailable'; END IF;
 id:=bpc_security.register_identity(p_issuer,p_subject);
 PERFORM set_config('app.identity_id',id::text,true);
 -- Shares the same identity row lock as bootstrap; history is never erased.
 SELECT status INTO st FROM identity.identities WHERE identity_id=id FOR UPDATE;
 IF st IS DISTINCT FROM 'Active' OR EXISTS(SELECT 1 FROM identity.identity_consumptions WHERE identity_id=id) THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Reference unavailable'; END IF;
 SELECT coalesce(array_agg(t),ARRAY[]::timestamptz[]) INTO times FROM identity.reference_emissions e CROSS JOIN LATERAL unnest(e.emitted_at) t WHERE e.identity_id=id AND t>now_at-interval '1 hour';
 IF cardinality(times)>=5 THEN RAISE EXCEPTION USING ERRCODE='P1429',MESSAGE='Issuance limit'; END IF;
 INSERT INTO identity.reference_emissions(identity_id,emitted_at) VALUES(id,array_append(times,now_at)) ON CONFLICT(identity_id) DO UPDATE SET emitted_at=EXCLUDED.emitted_at;
 expires:=now_at+interval '24 hours';
 INSERT INTO identity.owner_references(reference_hash,identity_id,display_name,expires_at) VALUES(p_hash,id,btrim(p_display_name),expires);
 RETURN jsonb_build_object('userId',id,'expiresAt',expires);
END $$;
ALTER FUNCTION bpc_security.issue_reference(text,text,bytea,text) OWNER TO bpc_e1_identity_exec;
CREATE FUNCTION bpc_security.preview_owner(p_user uuid,p_hash bytea) RETURNS jsonb LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE name text;
BEGIN
 PERFORM set_config('app.identity_id',p_user::text,true);
 SELECT r.display_name INTO name FROM identity.owner_references r JOIN identity.identities i ON i.identity_id=r.identity_id WHERE r.identity_id=p_user AND r.reference_hash=p_hash AND r.expires_at>statement_timestamp() AND i.status='Active' AND NOT EXISTS(SELECT 1 FROM identity.identity_consumptions c WHERE c.identity_id=p_user);
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Reference unavailable'; END IF;
 RETURN jsonb_build_object('userId',p_user,'displayName',name);
END $$;
ALTER FUNCTION bpc_security.preview_owner(uuid,bytea) OWNER TO bpc_e1_boot_exec;
CREATE FUNCTION bpc_security.platform_bootstrap(p_operator_issuer text,p_operator_subject text,p_user uuid,p_hash bytea,p_name text,p_operation uuid) RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE actor uuid; locked_id uuid; r record; result jsonb; internal_op uuid; st text;
BEGIN
 IF p_operation IS NULL OR p_user IS NULL OR p_hash IS NULL OR octet_length(p_hash)<>32 OR p_name IS NULL OR length(btrim(p_name)) NOT BETWEEN 1 AND 200 THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Reference unavailable'; END IF;
 -- The API must revalidate the real IdP Platform role and MFA before every call.
 PERFORM pg_advisory_xact_lock(hashtextextended(length(p_operator_issuer)::text||':'||p_operator_issuer||length(p_operator_subject)::text||':'||p_operator_subject||p_operation::text,4));
 actor:=bpc_security.register_identity(p_operator_issuer,p_operator_subject);
 PERFORM set_config('app.platform_actor_id',actor::text,true);
 -- All Platform calls acquire the operation lock before globally ordered rows.
 FOR locked_id IN SELECT DISTINCT x FROM unnest(ARRAY[actor,p_user]) x ORDER BY x LOOP
  PERFORM set_config('app.identity_id',locked_id::text,true);
  PERFORM identity_id FROM identity.identities WHERE identity_id=locked_id FOR UPDATE;
 END LOOP;
 PERFORM set_config('app.identity_id',actor::text,true);
 SELECT status INTO st FROM identity.identities WHERE identity_id=actor;
 IF st IS DISTINCT FROM 'Active' THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Reference unavailable'; END IF;
 PERFORM set_config('app.identity_id',p_user::text,true);
 SELECT status INTO st FROM identity.identities WHERE identity_id=p_user;
 IF st IS DISTINCT FROM 'Active' THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Reference unavailable'; END IF;
 SELECT * INTO r FROM bpc_security.platform_receipts WHERE actor_identity_id=actor AND operation_id=p_operation;
 IF FOUND THEN
  IF r.target_identity_id<>p_user OR r.reference_hash<>p_hash OR r.organization_name<>p_name THEN RAISE EXCEPTION USING ERRCODE='P1409',MESSAGE='Operation conflict'; END IF;
  -- Replay permits only the consumption made by this operation; possession
  -- still requires the original matching capability within its 24-hour TTL.
  IF NOT EXISTS(SELECT 1 FROM identity.owner_references WHERE identity_id=p_user AND reference_hash=p_hash AND expires_at>clock_timestamp()) THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Reference unavailable'; END IF;
  RETURN r.result||jsonb_build_object('replayed',true);
 END IF;
 PERFORM bpc_security.preview_owner(p_user,p_hash);
 internal_op:=gen_random_uuid();
 result:=bpc_security.bootstrap_organization(p_user,p_name,internal_op);
 INSERT INTO bpc_security.platform_events(event_id,actor_identity_id,target_identity_id,organization_id,operation_id,kind) VALUES(gen_random_uuid(),actor,p_user,(result->>'organizationId')::uuid,p_operation,'OrganizationBootstrapped');
 INSERT INTO bpc_security.platform_receipts VALUES(actor,p_operation,p_user,p_hash,p_name,internal_op,result);
 RETURN result||jsonb_build_object('replayed',false);
END $$;
ALTER FUNCTION bpc_security.platform_bootstrap(text,text,uuid,bytea,text,uuid) OWNER TO bpc_e1_boot_exec;
REVOKE ALL ON FUNCTION bpc_security.issue_reference(text,text,bytea,text),bpc_security.preview_owner(uuid,bytea),bpc_security.platform_bootstrap(text,text,uuid,bytea,text,uuid) FROM PUBLIC;
GRANT UPDATE(status) ON identity.identities TO bpc_e1_identity_exec;

-- Existing rows remain immutable. Platform calls attribute new events to the real operator.
CREATE OR REPLACE FUNCTION bpc_security.bootstrap_organization(p_identity uuid,p_name text,p_operation uuid) RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE r record; org uuid:=gen_random_uuid(); acc uuid:=gen_random_uuid(); mem uuid:=gen_random_uuid(); st text;
BEGIN
 PERFORM set_config('app.identity_id',p_identity::text,true); PERFORM set_config('app.operation_id',p_operation::text,true);
 PERFORM pg_advisory_xact_lock(hashtextextended(p_operation::text,1));
 SELECT status INTO st FROM identity.identities WHERE identity_id=p_identity FOR UPDATE;
 IF st IS DISTINCT FROM 'Active' THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Access unavailable'; END IF;

 SELECT operation_id,target_identity_id,organization_id,account_id,membership_id,organization_name INTO r FROM bpc_security.operation_receipts WHERE operation_id=p_operation;
 IF FOUND THEN
  IF r.target_identity_id<>p_identity OR r.organization_name<>p_name THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Operation conflict'; END IF;
  RETURN jsonb_build_object('organizationId',r.organization_id,'accountId',r.account_id,'membershipId',r.membership_id);
 END IF;
 IF EXISTS(SELECT 1 FROM identity.identity_consumptions WHERE identity_id=p_identity) THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Identity consumed'; END IF;
 PERFORM set_config('app.identity_id',p_identity::text,true); PERFORM set_config('app.organization_id',org::text,true);
 INSERT INTO tenancy.organizations(organization_id,name,status) VALUES(org,p_name,'Active');
 INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id,origin) VALUES(p_identity,org,acc,'PlatformBootstrap');
 INSERT INTO identity.enterprise_user_accounts(account_id,identity_id,organization_id,status) VALUES(acc,p_identity,org,'Active');
 INSERT INTO tenancy.memberships(membership_id,organization_id,account_id,status,role) VALUES(mem,org,acc,'Active','Owner');
 INSERT INTO bpc_security.events(event_id,operation_id,organization_id,actor_id,target_account_id,kind) VALUES(gen_random_uuid(),p_operation,org,coalesce(bpc_security.ctx('app.platform_actor_id'),p_identity),acc,'OrganizationBootstrapped');
 INSERT INTO bpc_security.operation_receipts VALUES(p_operation,p_identity,org,acc,mem,p_name);
 RETURN jsonb_build_object('organizationId',org,'accountId',acc,'membershipId',mem);
END $$;

CREATE OR REPLACE FUNCTION bpc_security.persistence_ready() RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT (SELECT array_agg(version ORDER BY version)=ARRAY[1,2,3,4] AND bool_and(length(checksum)=64) FROM bpc_migrations.versions)
 AND (SELECT count(*)=12 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='bpc_e1_ddl') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security') AND c.relkind='r')
 AND (SELECT count(*)=6 AND bool_and(NOT rolsuper AND NOT rolbypassrls AND NOT rolcanlogin AND NOT rolcreaterole AND NOT rolcreatedb) FROM pg_roles WHERE rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.roleid WHERE r.rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace CROSS JOIN LATERAL aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) a WHERE n.nspname='bpc_security' AND a.grantee=0 AND a.privilege_type='EXECUTE')
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='bpc_security' AND (p.prosecdef AND pg_get_userbyid(p.proowner)='bpc_e1_ddl' OR NOT coalesce(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp'],false)))
$$;
