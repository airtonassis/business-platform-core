-- Product E1 migration. Applied explicitly, inside the migrator transaction.
-- Runtime never owns tables/functions and never receives membership of these owners.
CREATE ROLE bpc_e1_ddl NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE bpc_e1_identity_exec NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE bpc_e1_tenant_exec NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE bpc_e1_boot_exec NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE bpc_e1_integrity_exec NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE bpc_e1_context_exec NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE SCHEMA identity AUTHORIZATION bpc_e1_ddl;
CREATE SCHEMA tenancy AUTHORIZATION bpc_e1_ddl;
CREATE SCHEMA bpc_security AUTHORIZATION bpc_e1_ddl;
CREATE SCHEMA bpc_migrations AUTHORIZATION bpc_e1_ddl;
REVOKE ALL ON SCHEMA identity,tenancy,bpc_security,bpc_migrations FROM PUBLIC;
SET LOCAL ROLE bpc_e1_ddl;
ALTER DEFAULT PRIVILEGES REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
CREATE TABLE bpc_migrations.versions(version integer PRIMARY KEY, checksum text NOT NULL CHECK(length(checksum)=64), applied_at timestamptz NOT NULL DEFAULT clock_timestamp());
CREATE TABLE identity.identities(identity_id uuid PRIMARY KEY, status text NOT NULL CHECK(status IN ('Active','Blocked','Archived')), created_at timestamptz NOT NULL DEFAULT clock_timestamp());
CREATE TABLE identity.identity_links(link_id uuid PRIMARY KEY, identity_id uuid NOT NULL REFERENCES identity.identities ON DELETE RESTRICT,
 issuer text COLLATE "C" NOT NULL CHECK(length(issuer) BETWEEN 1 AND 2048), subject text COLLATE "C" NOT NULL CHECK(length(subject) BETWEEN 1 AND 512), UNIQUE(issuer,subject), UNIQUE(identity_id));
CREATE TABLE tenancy.organizations(organization_id uuid PRIMARY KEY, name text NOT NULL CHECK(length(btrim(name)) BETWEEN 1 AND 200), status text NOT NULL CHECK(status IN ('Active','Blocked','Archived')), version bigint NOT NULL DEFAULT 1 CHECK(version>0));
CREATE TABLE identity.identity_consumptions(identity_id uuid PRIMARY KEY REFERENCES identity.identities ON DELETE RESTRICT,
 organization_id uuid NOT NULL REFERENCES tenancy.organizations ON DELETE RESTRICT, account_id uuid NOT NULL UNIQUE,
 consumed_at timestamptz NOT NULL DEFAULT clock_timestamp(), origin text NOT NULL CHECK(origin='PlatformBootstrap'), UNIQUE(identity_id,organization_id,account_id));
CREATE INDEX consumptions_org ON identity.identity_consumptions(organization_id);
CREATE TABLE identity.enterprise_user_accounts(account_id uuid PRIMARY KEY, identity_id uuid NOT NULL UNIQUE REFERENCES identity.identities ON DELETE RESTRICT,
 organization_id uuid NOT NULL REFERENCES tenancy.organizations ON DELETE RESTRICT, status text NOT NULL CHECK(status IN ('Active','Revoked','Archived')), version bigint NOT NULL DEFAULT 1 CHECK(version>0),
 UNIQUE(organization_id,account_id), FOREIGN KEY(identity_id,organization_id,account_id) REFERENCES identity.identity_consumptions(identity_id,organization_id,account_id) DEFERRABLE INITIALLY DEFERRED);
CREATE INDEX accounts_org_status ON identity.enterprise_user_accounts(organization_id,status);
CREATE TABLE tenancy.memberships(membership_id uuid PRIMARY KEY, organization_id uuid NOT NULL, account_id uuid NOT NULL UNIQUE,
 status text NOT NULL CHECK(status IN ('Active','Revoked')), role text NOT NULL CHECK(role IN ('Owner','Admin','Member')), version bigint NOT NULL DEFAULT 1 CHECK(version>0),
 UNIQUE(organization_id,membership_id), FOREIGN KEY(organization_id,account_id) REFERENCES identity.enterprise_user_accounts(organization_id,account_id) ON DELETE RESTRICT);
CREATE INDEX memberships_org_status ON tenancy.memberships(organization_id,status);
CREATE TABLE bpc_security.operation_receipts(operation_id uuid PRIMARY KEY, target_identity_id uuid NOT NULL, organization_id uuid NOT NULL, account_id uuid NOT NULL, membership_id uuid NOT NULL, organization_name text NOT NULL);
CREATE TABLE bpc_security.events(event_id uuid PRIMARY KEY, operation_id uuid NOT NULL, organization_id uuid NOT NULL, actor_id uuid NOT NULL, target_account_id uuid NOT NULL, kind text NOT NULL CHECK(kind IN ('OrganizationBootstrapped','MembershipRevoked')), occurred_at timestamptz NOT NULL DEFAULT clock_timestamp());
CREATE INDEX events_org_time ON bpc_security.events(organization_id,occurred_at);
CREATE FUNCTION bpc_security.ctx(key text) RETURNS uuid LANGUAGE sql STABLE SET search_path=pg_catalog,pg_temp AS $$ SELECT nullif(pg_catalog.current_setting(key,true),'')::uuid $$;
-- Guards remain INVOKER: trigger ownership must not silently elevate a DML caller.
CREATE FUNCTION bpc_security.immutable_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION USING ERRCODE='P1001', MESSAGE='Permanent record'; END IF;
 IF TG_TABLE_NAME IN ('identity_consumptions','identity_links') THEN RAISE EXCEPTION USING ERRCODE='P1001', MESSAGE='Permanent record'; END IF;
 IF TG_TABLE_NAME='enterprise_user_accounts' THEN
 IF NEW.identity_id<>OLD.identity_id OR NEW.organization_id<>OLD.organization_id OR NEW.account_id<>OLD.account_id THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Immutable binding'; END IF;
 END IF;
 IF TG_TABLE_NAME='memberships' THEN
  IF NEW.membership_id<>OLD.membership_id OR NEW.organization_id<>OLD.organization_id OR NEW.account_id<>OLD.account_id OR OLD.role='Owner' OR NEW.role='Owner' OR (OLD.status='Revoked' AND NEW.status<>'Revoked') THEN
   RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Protected membership'; END IF;
 END IF;
 IF TG_TABLE_NAME IN ('enterprise_user_accounts','memberships','organizations') THEN
  IF NEW.version<>OLD.version+1 THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Invalid version'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER consumption_immutable BEFORE UPDATE OR DELETE ON identity.identity_consumptions FOR EACH ROW EXECUTE FUNCTION bpc_security.immutable_guard();
CREATE TRIGGER link_immutable BEFORE UPDATE OR DELETE ON identity.identity_links FOR EACH ROW EXECUTE FUNCTION bpc_security.immutable_guard();
CREATE TRIGGER account_immutable BEFORE UPDATE OR DELETE ON identity.enterprise_user_accounts FOR EACH ROW EXECUTE FUNCTION bpc_security.immutable_guard();
CREATE TRIGGER membership_protected BEFORE UPDATE OR DELETE ON tenancy.memberships FOR EACH ROW EXECUTE FUNCTION bpc_security.immutable_guard();
CREATE TRIGGER organization_protected BEFORE UPDATE OR DELETE ON tenancy.organizations FOR EACH ROW EXECUTE FUNCTION bpc_security.immutable_guard();
RESET ROLE;
GRANT USAGE ON SCHEMA identity,tenancy,bpc_security TO bpc_e1_identity_exec,bpc_e1_tenant_exec,bpc_e1_boot_exec,bpc_e1_integrity_exec,bpc_e1_context_exec;
GRANT EXECUTE ON FUNCTION bpc_security.ctx(text) TO bpc_e1_identity_exec,bpc_e1_tenant_exec,bpc_e1_boot_exec,bpc_e1_integrity_exec,bpc_e1_context_exec;
-- Integrity is independent of authorization after revocation; no locks or repair here.
CREATE FUNCTION bpc_security.validate_integrity() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE org uuid;
BEGIN
 org:=NEW.organization_id;
 PERFORM set_config('app.integrity_org',org::text,true);
 IF EXISTS(SELECT 1 FROM identity.enterprise_user_accounts a LEFT JOIN tenancy.memberships m ON m.organization_id=a.organization_id AND m.account_id=a.account_id WHERE a.organization_id=org AND ((a.status='Active' AND (m.membership_id IS NULL OR m.status<>'Active')) OR (a.status<>'Active' AND m.status='Active'))) THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Incoherent membership'; END IF;
 IF EXISTS(SELECT 1 FROM tenancy.organizations o WHERE o.organization_id=org AND o.status='Active') AND NOT EXISTS(SELECT 1 FROM tenancy.memberships m JOIN identity.enterprise_user_accounts a USING(organization_id,account_id) WHERE m.organization_id=org AND m.role='Owner' AND m.status='Active' AND a.status='Active') THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Owner required'; END IF;
 RETURN NULL;
END $$;
ALTER FUNCTION bpc_security.validate_integrity() OWNER TO bpc_e1_integrity_exec;
CREATE CONSTRAINT TRIGGER account_integrity AFTER INSERT OR UPDATE ON identity.enterprise_user_accounts DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION bpc_security.validate_integrity();
CREATE CONSTRAINT TRIGGER membership_integrity AFTER INSERT OR UPDATE ON tenancy.memberships DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION bpc_security.validate_integrity();
CREATE CONSTRAINT TRIGGER organization_integrity AFTER INSERT OR UPDATE ON tenancy.organizations DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION bpc_security.validate_integrity();
GRANT SELECT(organization_id,status) ON tenancy.organizations TO bpc_e1_integrity_exec;
GRANT SELECT(organization_id,account_id,status) ON identity.enterprise_user_accounts TO bpc_e1_integrity_exec;
GRANT SELECT(organization_id,account_id,membership_id,status,role) ON tenancy.memberships TO bpc_e1_integrity_exec;
-- Every business/history table uses FORCE; privileged installer remains an explicit trust boundary.
DO $$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['identity.identities','identity.identity_links','identity.identity_consumptions','identity.enterprise_user_accounts','tenancy.organizations','tenancy.memberships','bpc_security.operation_receipts','bpc_security.events'] LOOP EXECUTE format('ALTER TABLE %s ENABLE ROW LEVEL SECURITY',t); EXECUTE format('ALTER TABLE %s FORCE ROW LEVEL SECURITY',t); END LOOP; END $$;
-- Narrow SELF for identity resolver; tenant/boot scopes are installed only by their ports.
CREATE POLICY self_identity ON identity.identities TO bpc_e1_identity_exec USING(identity_id=bpc_security.ctx('app.identity_id')) WITH CHECK(identity_id=bpc_security.ctx('app.identity_id'));
CREATE POLICY self_link ON identity.identity_links TO bpc_e1_identity_exec USING(issuer=current_setting('app.issuer',true) AND subject=current_setting('app.subject',true)) WITH CHECK(identity_id=bpc_security.ctx('app.identity_id') AND issuer=current_setting('app.issuer',true) AND subject=current_setting('app.subject',true));
CREATE POLICY self_consumption ON identity.identity_consumptions TO bpc_e1_identity_exec USING(identity_id=bpc_security.ctx('app.identity_id'));
CREATE POLICY self_account ON identity.enterprise_user_accounts TO bpc_e1_identity_exec USING(identity_id=bpc_security.ctx('app.identity_id'));
CREATE POLICY self_membership ON tenancy.memberships TO bpc_e1_identity_exec USING(account_id=bpc_security.ctx('app.account_id'));
CREATE POLICY self_org ON tenancy.organizations TO bpc_e1_identity_exec USING(organization_id=bpc_security.ctx('app.organization_id'));
CREATE POLICY boot_identity ON identity.identities TO bpc_e1_boot_exec USING(identity_id=bpc_security.ctx('app.identity_id'));
CREATE POLICY boot_consumption ON identity.identity_consumptions TO bpc_e1_boot_exec USING(identity_id=bpc_security.ctx('app.identity_id')) WITH CHECK(identity_id=bpc_security.ctx('app.identity_id'));
DO $$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['identity.enterprise_user_accounts','tenancy.organizations','tenancy.memberships','bpc_security.events'] LOOP EXECUTE format('CREATE POLICY boot_scope ON %s TO bpc_e1_boot_exec USING (organization_id=bpc_security.ctx(''app.organization_id'')) WITH CHECK (organization_id=bpc_security.ctx(''app.organization_id''))',t); END LOOP; END $$;
CREATE POLICY boot_receipt ON bpc_security.operation_receipts TO bpc_e1_boot_exec USING(operation_id=bpc_security.ctx('app.operation_id')) WITH CHECK(operation_id=bpc_security.ctx('app.operation_id'));
CREATE POLICY integrity_accounts ON identity.enterprise_user_accounts TO bpc_e1_integrity_exec USING(organization_id=bpc_security.ctx('app.integrity_org'));
CREATE POLICY integrity_members ON tenancy.memberships TO bpc_e1_integrity_exec USING(organization_id=bpc_security.ctx('app.integrity_org'));
CREATE POLICY integrity_org ON tenancy.organizations TO bpc_e1_integrity_exec USING(organization_id=bpc_security.ctx('app.integrity_org'));
DO $$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['identity.enterprise_user_accounts','tenancy.memberships','tenancy.organizations','bpc_security.events'] LOOP EXECUTE format('CREATE POLICY tenant_scope ON %s TO bpc_e1_tenant_exec USING (organization_id=bpc_security.ctx(''app.organization_id'')) WITH CHECK (organization_id=bpc_security.ctx(''app.organization_id''))',t); END LOOP; END $$;
CREATE POLICY tenant_identity ON identity.identities TO bpc_e1_tenant_exec USING(identity_id=bpc_security.ctx('app.identity_id'));
CREATE POLICY tenant_consumption ON identity.identity_consumptions TO bpc_e1_tenant_exec USING(identity_id=bpc_security.ctx('app.identity_id'));
GRANT SELECT(identity_id,status),INSERT(identity_id,status) ON identity.identities TO bpc_e1_identity_exec;
GRANT SELECT(link_id,identity_id,issuer,subject),INSERT(link_id,identity_id,issuer,subject) ON identity.identity_links TO bpc_e1_identity_exec;
GRANT SELECT(identity_id,organization_id,account_id) ON identity.identity_consumptions TO bpc_e1_identity_exec;
GRANT SELECT(account_id,identity_id,organization_id,status) ON identity.enterprise_user_accounts TO bpc_e1_identity_exec;
GRANT SELECT(membership_id,account_id,organization_id,status,role) ON tenancy.memberships TO bpc_e1_identity_exec;
GRANT SELECT(organization_id,name,status) ON tenancy.organizations TO bpc_e1_identity_exec;
CREATE FUNCTION bpc_security.register_identity(p_issuer text,p_subject text) RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE id uuid;
BEGIN
 PERFORM set_config('app.issuer',p_issuer,true); PERFORM set_config('app.subject',p_subject,true);
 -- Serialize the exact key without leaking plaintext in locks or durable records.
 PERFORM pg_advisory_xact_lock(hashtextextended(length(p_issuer)::text||':'||p_issuer||p_subject,0));
 SELECT identity_id INTO id FROM identity.identity_links WHERE issuer=p_issuer AND subject=p_subject;
 IF id IS NULL THEN
  id:=gen_random_uuid(); PERFORM set_config('app.identity_id',id::text,true);
  INSERT INTO identity.identities(identity_id,status) VALUES(id,'Active');
  INSERT INTO identity.identity_links(link_id,identity_id,issuer,subject) VALUES(gen_random_uuid(),id,p_issuer,p_subject);
 END IF;
 RETURN id;
END $$;
ALTER FUNCTION bpc_security.register_identity(text,text) OWNER TO bpc_e1_identity_exec;
CREATE FUNCTION bpc_security.resolve_context(p_issuer text,p_subject text) RETURNS jsonb LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE id uuid; st text; h record; a record; m record; o record;
BEGIN
 PERFORM set_config('app.identity_id','',true); PERFORM set_config('app.account_id','',true); PERFORM set_config('app.organization_id','',true);
 PERFORM set_config('app.issuer',p_issuer,true); PERFORM set_config('app.subject',p_subject,true);
 SELECT identity_id INTO id FROM identity.identity_links WHERE issuer=p_issuer AND subject=p_subject;
 IF id IS NULL THEN RETURN jsonb_build_object('state','Inconsistent'); END IF;
 PERFORM set_config('app.identity_id',id::text,true);
 SELECT status INTO st FROM identity.identities WHERE identity_id=id;
 IF st IS DISTINCT FROM 'Active' THEN RETURN jsonb_build_object('state','Denied'); END IF;
 SELECT identity_id,organization_id,account_id INTO h FROM identity.identity_consumptions WHERE identity_id=id;
 IF NOT FOUND THEN RETURN jsonb_build_object('state','NotLinked','identityId',id); END IF;
 PERFORM set_config('app.organization_id',h.organization_id::text,true); PERFORM set_config('app.account_id',h.account_id::text,true);
 SELECT account_id,identity_id,organization_id,status INTO a FROM identity.enterprise_user_accounts WHERE identity_id=id;
 IF NOT FOUND THEN RETURN jsonb_build_object('state','Inconsistent'); END IF;
 SELECT membership_id,account_id,organization_id,status,role INTO m FROM tenancy.memberships WHERE account_id=a.account_id;
 IF NOT FOUND THEN RETURN jsonb_build_object('state','Inconsistent'); END IF;
 SELECT organization_id,name,status INTO o FROM tenancy.organizations WHERE organization_id=a.organization_id;
 IF NOT FOUND OR a.account_id<>h.account_id OR a.organization_id<>h.organization_id OR m.organization_id<>h.organization_id THEN RETURN jsonb_build_object('state','Inconsistent'); END IF;
 IF a.status<>'Active' OR m.status<>'Active' OR o.status<>'Active' THEN RETURN jsonb_build_object('state','Denied'); END IF;
 RETURN jsonb_build_object('state','Linked','identityId',id,'accountId',a.account_id,'organizationId',a.organization_id,'membershipId',m.membership_id,'role',m.role,'organizationName',o.name);
END $$;
ALTER FUNCTION bpc_security.resolve_context(text,text) OWNER TO bpc_e1_identity_exec;
GRANT SELECT(identity_id,status),UPDATE(status) ON identity.identities TO bpc_e1_boot_exec;
GRANT SELECT(identity_id,organization_id,account_id),INSERT(identity_id,organization_id,account_id,origin) ON identity.identity_consumptions TO bpc_e1_boot_exec;
GRANT INSERT(organization_id,name,status),SELECT(organization_id,name,status) ON tenancy.organizations TO bpc_e1_boot_exec;
GRANT INSERT(account_id,identity_id,organization_id,status) ON identity.enterprise_user_accounts TO bpc_e1_boot_exec;
GRANT INSERT(membership_id,organization_id,account_id,status,role) ON tenancy.memberships TO bpc_e1_boot_exec;
GRANT SELECT(operation_id,target_identity_id,organization_id,account_id,membership_id,organization_name),INSERT(operation_id,target_identity_id,organization_id,account_id,membership_id,organization_name) ON bpc_security.operation_receipts TO bpc_e1_boot_exec;
GRANT INSERT(event_id,operation_id,organization_id,actor_id,target_account_id,kind) ON bpc_security.events TO bpc_e1_boot_exec,bpc_e1_tenant_exec;
CREATE FUNCTION bpc_security.bootstrap_organization(p_identity uuid,p_name text,p_operation uuid) RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE r record; org uuid:=gen_random_uuid(); acc uuid:=gen_random_uuid(); mem uuid:=gen_random_uuid(); st text;
BEGIN
 PERFORM set_config('app.identity_id',p_identity::text,true); PERFORM set_config('app.operation_id',p_operation::text,true);
 PERFORM pg_advisory_xact_lock(hashtextextended(p_operation::text,1));
 SELECT operation_id,target_identity_id,organization_id,account_id,membership_id,organization_name INTO r FROM bpc_security.operation_receipts WHERE operation_id=p_operation;
 IF FOUND THEN
  IF r.target_identity_id<>p_identity OR r.organization_name<>p_name THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Operation conflict'; END IF;
  RETURN jsonb_build_object('organizationId',r.organization_id,'accountId',r.account_id,'membershipId',r.membership_id);
 END IF;
 SELECT status INTO st FROM identity.identities WHERE identity_id=p_identity FOR UPDATE;
 IF st IS DISTINCT FROM 'Active' THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Access unavailable'; END IF;
 IF EXISTS(SELECT 1 FROM identity.identity_consumptions WHERE identity_id=p_identity) THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Identity consumed'; END IF;
 PERFORM set_config('app.identity_id',p_identity::text,true); PERFORM set_config('app.organization_id',org::text,true);
 INSERT INTO tenancy.organizations(organization_id,name,status) VALUES(org,p_name,'Active');
 INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id,origin) VALUES(p_identity,org,acc,'PlatformBootstrap');
 INSERT INTO identity.enterprise_user_accounts(account_id,identity_id,organization_id,status) VALUES(acc,p_identity,org,'Active');
 INSERT INTO tenancy.memberships(membership_id,organization_id,account_id,status,role) VALUES(mem,org,acc,'Active','Owner');
 INSERT INTO bpc_security.events(event_id,operation_id,organization_id,actor_id,target_account_id,kind) VALUES(gen_random_uuid(),p_operation,org,p_identity,acc,'OrganizationBootstrapped');
 INSERT INTO bpc_security.operation_receipts VALUES(p_operation,p_identity,org,acc,mem,p_name);
 RETURN jsonb_build_object('organizationId',org,'accountId',acc,'membershipId',mem);
END $$;
ALTER FUNCTION bpc_security.bootstrap_organization(uuid,text,uuid) OWNER TO bpc_e1_boot_exec;
-- No invitation table/port is installed in E1. Runtime has no schema CREATE, broad
-- INSERT or accept transition; the P1-02 capability is absent, not claimed as tested invitation flow.
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA bpc_security FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA identity,tenancy,bpc_security,bpc_migrations FROM PUBLIC;
GRANT SELECT(identity_id,status),UPDATE(status) ON identity.identities TO bpc_e1_tenant_exec;
GRANT SELECT(organization_id,status,version),UPDATE(version) ON tenancy.organizations TO bpc_e1_tenant_exec;
GRANT SELECT(account_id,identity_id,organization_id,status,version),UPDATE(status,version) ON identity.enterprise_user_accounts TO bpc_e1_tenant_exec;
GRANT SELECT(membership_id,account_id,organization_id,status,role,version),UPDATE(status,version) ON tenancy.memberships TO bpc_e1_tenant_exec;
CREATE FUNCTION bpc_security.revoke_membership(p_actor uuid,p_org uuid,p_target uuid,p_version bigint,p_operation uuid) RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE actor record; target record; st text;
BEGIN
 PERFORM set_config('app.identity_id',p_actor::text,true); PERFORM set_config('app.organization_id',p_org::text,true);
 SELECT status INTO st FROM identity.identities WHERE identity_id=p_actor FOR SHARE;
 IF st IS DISTINCT FROM 'Active' THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Access unavailable'; END IF;
 PERFORM 1 FROM tenancy.organizations WHERE organization_id=p_org AND status='Active' FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Access unavailable'; END IF;
 -- Tables locked separately, UUID order; no late validator locks.
 PERFORM 1 FROM identity.enterprise_user_accounts WHERE organization_id=p_org ORDER BY account_id FOR UPDATE;
 PERFORM 1 FROM tenancy.memberships WHERE organization_id=p_org ORDER BY membership_id FOR UPDATE;
 SELECT a.account_id,a.status AS account_status,m.status,m.role INTO actor FROM identity.enterprise_user_accounts a JOIN tenancy.memberships m USING(organization_id,account_id) WHERE a.identity_id=p_actor AND a.organization_id=p_org;
 SELECT a.account_id,a.status AS account_status,m.status,m.role,m.version INTO target FROM identity.enterprise_user_accounts a JOIN tenancy.memberships m USING(organization_id,account_id) WHERE m.membership_id=p_target AND a.organization_id=p_org;
 IF actor.account_id IS NULL OR target.account_id IS NULL OR actor.account_status<>'Active' OR actor.status<>'Active' OR actor.role NOT IN ('Owner','Admin') OR target.role='Owner' OR target.account_id=actor.account_id OR (actor.role='Admin' AND target.role<>'Member') OR target.status<>'Active' OR target.account_status<>'Active' THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Access unavailable'; END IF;
 IF target.version<>p_version THEN RAISE EXCEPTION USING ERRCODE='P1002',MESSAGE='Version conflict'; END IF;
 UPDATE identity.enterprise_user_accounts SET status='Revoked',version=version+1 WHERE account_id=target.account_id AND organization_id=p_org;
 UPDATE tenancy.memberships SET status='Revoked',version=version+1 WHERE membership_id=p_target AND organization_id=p_org;
 INSERT INTO bpc_security.events(event_id,operation_id,organization_id,actor_id,target_account_id,kind) VALUES(gen_random_uuid(),p_operation,p_org,p_actor,target.account_id,'MembershipRevoked');
END $$;
ALTER FUNCTION bpc_security.revoke_membership(uuid,uuid,uuid,bigint,uuid) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.revoke_membership(uuid,uuid,uuid,bigint,uuid) FROM PUBLIC;
GRANT USAGE ON SCHEMA bpc_migrations TO bpc_e1_context_exec;
GRANT SELECT(version,checksum) ON bpc_migrations.versions TO bpc_e1_context_exec;
CREATE FUNCTION bpc_security.persistence_ready() RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT EXISTS(SELECT 1 FROM bpc_migrations.versions WHERE version=1 AND length(checksum)=64)
 AND (SELECT count(*)=8 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='bpc_e1_ddl') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security') AND c.relkind='r')
 AND NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname IN ('bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec') AND (rolsuper OR rolbypassrls OR rolcanlogin))
$$;
ALTER FUNCTION bpc_security.persistence_ready() OWNER TO bpc_e1_context_exec;
REVOKE ALL ON FUNCTION bpc_security.persistence_ready() FROM PUBLIC;
