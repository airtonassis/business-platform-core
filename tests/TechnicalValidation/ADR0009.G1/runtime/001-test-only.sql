-- TEST-ONLY migration. Never used by production migrations.
-- Requires a newly-created, marked, disposable database; see RunValidation.
DO $$ BEGIN
 IF current_database() !~ '^bpc_adr0009_g1_[a-f0-9]{12}$'
 OR obj_description((SELECT oid FROM pg_database WHERE datname=current_database()),'pg_database') <> 'bpc-adr0009-g1:disposable'
 THEN RAISE EXCEPTION 'isolated target required'; END IF;
 IF EXISTS(SELECT FROM pg_namespace WHERE nspname='tenancy') THEN RAISE EXCEPTION 'fresh database required'; END IF;
END $$;
CREATE ROLE ddl_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE core_migrator NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE exec_identity NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE exec_tenant NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE exec_bootstrap NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE exec_context NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE exec_integrity NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE core_identity LOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE core_tenant LOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE core_platform LOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
CREATE ROLE g1_probe LOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
-- Database CONNECT grants are installed separately with the guarded identifier.
-- The harness grants CONNECT to the named logins using the validated DB identifier.
CREATE SCHEMA identity AUTHORIZATION ddl_owner;
CREATE SCHEMA tenancy AUTHORIZATION ddl_owner;
CREATE SCHEMA sandbox AUTHORIZATION ddl_owner;
CREATE SCHEMA audit AUTHORIZATION ddl_owner;
CREATE SCHEMA operation AUTHORIZATION ddl_owner;
CREATE SCHEMA authz AUTHORIZATION ddl_owner;
SET ROLE ddl_owner;
ALTER DEFAULT PRIVILEGES REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;

CREATE TABLE identity.identities(
 id uuid PRIMARY KEY, status text NOT NULL CHECK(status IN('Active','Blocked','Archived')),
 version bigint NOT NULL DEFAULT 1 CHECK(version>0));
CREATE TABLE identity.identity_links(
 id uuid PRIMARY KEY, identity_id uuid NOT NULL REFERENCES identity.identities(id),
 issuer text NOT NULL CHECK(length(issuer)>0), subject text NOT NULL CHECK(length(subject)>0),
 UNIQUE(issuer,subject));
CREATE TABLE tenancy.organizations(
 id uuid PRIMARY KEY, name text NOT NULL CHECK(length(name) BETWEEN 1 AND 200),
 status text NOT NULL CHECK(status IN('Active','Blocked','Archived')), version bigint NOT NULL DEFAULT 1 CHECK(version>0));
CREATE TABLE identity.identity_consumptions(
 identity_id uuid PRIMARY KEY REFERENCES identity.identities(id),
 organization_id uuid NOT NULL REFERENCES tenancy.organizations(id), account_id uuid NOT NULL UNIQUE,
 consumed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
 UNIQUE(identity_id,organization_id,account_id));
CREATE TABLE identity.enterprise_user_accounts(
 id uuid PRIMARY KEY, identity_id uuid NOT NULL UNIQUE, organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
 status text NOT NULL CHECK(status IN('Active','Revoked','Archived')), version bigint NOT NULL DEFAULT 1 CHECK(version>0),
 UNIQUE(organization_id,id),
 FOREIGN KEY(identity_id,organization_id,id) REFERENCES identity.identity_consumptions(identity_id,organization_id,account_id)
 DEFERRABLE INITIALLY DEFERRED);
CREATE TABLE tenancy.memberships(
 id uuid PRIMARY KEY, organization_id uuid NOT NULL, account_id uuid NOT NULL UNIQUE,
 role text NOT NULL CHECK(role IN('Owner','Admin','Member')),
 status text NOT NULL CHECK(status IN('Active','Revoked')), version bigint NOT NULL DEFAULT 1 CHECK(version>0),
 UNIQUE(organization_id,id),
 FOREIGN KEY(organization_id,account_id) REFERENCES identity.enterprise_user_accounts(organization_id,id));
CREATE INDEX ON tenancy.memberships(organization_id,status,role,id);
CREATE TABLE tenancy.invitation_slots(
 organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
 recipient_identity_id uuid NOT NULL REFERENCES identity.identities(id), version bigint NOT NULL DEFAULT 1,
 PRIMARY KEY(organization_id,recipient_identity_id));
CREATE TABLE identity.recipient_references(
 identity_id uuid NOT NULL REFERENCES identity.identities(id), secret_hash bytea PRIMARY KEY,
 expires_at timestamptz NOT NULL);
CREATE TABLE tenancy.invitations(
 id uuid PRIMARY KEY, organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
 recipient_identity_id uuid NOT NULL REFERENCES identity.identities(id), issuer_membership_id uuid NOT NULL,
 secret_hash bytea NOT NULL UNIQUE, status text NOT NULL CHECK(status IN('Pending','Accepted','Expired','Cancelled','Replaced')),
 created_at timestamptz NOT NULL DEFAULT clock_timestamp(), expires_at timestamptz NOT NULL,
 accepted_account_id uuid, version bigint NOT NULL DEFAULT 1 CHECK(version>0),
 predecessor_id uuid UNIQUE, successor_id uuid UNIQUE,
 UNIQUE(organization_id,id),
 FOREIGN KEY(organization_id,issuer_membership_id) REFERENCES tenancy.memberships(organization_id,id),
 FOREIGN KEY(organization_id,accepted_account_id) REFERENCES identity.enterprise_user_accounts(organization_id,id),
 FOREIGN KEY(organization_id,predecessor_id) REFERENCES tenancy.invitations(organization_id,id),
 FOREIGN KEY(organization_id,successor_id) REFERENCES tenancy.invitations(organization_id,id),
 CHECK(expires_at>created_at), CHECK(status <> 'Accepted' OR accepted_account_id IS NOT NULL));
CREATE UNIQUE INDEX invitation_one_pending ON tenancy.invitations(organization_id,recipient_identity_id) WHERE status='Pending';
CREATE TABLE sandbox.demo_records(
 id uuid PRIMARY KEY, organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
 title text NOT NULL CHECK(length(title) BETWEEN 1 AND 200), version bigint NOT NULL DEFAULT 1 CHECK(version>0));
CREATE TABLE audit.events(
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), organization_id uuid REFERENCES tenancy.organizations(id),
 actor_id uuid NOT NULL, action text NOT NULL, operation_id uuid NOT NULL,
 occurred_at timestamptz NOT NULL DEFAULT clock_timestamp());
CREATE TABLE operation.idempotency_records(
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), actor_id uuid NOT NULL, scope_key text NOT NULL,
 operation text NOT NULL, key text NOT NULL, request_hash text NOT NULL, result_id uuid NOT NULL,
 UNIQUE(actor_id,scope_key,operation,key));

CREATE FUNCTION authz.ctx(k text) RETURNS uuid LANGUAGE sql STABLE
SET search_path=pg_catalog,pg_temp AS $$
 SELECT nullif(current_setting(k,true),'')::uuid $$;
REVOKE EXECUTE ON FUNCTION authz.ctx(text) FROM PUBLIC;

CREATE FUNCTION authz.own_tenant_allowed(write_required boolean) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT EXISTS(
 SELECT FROM identity.identities i
 JOIN identity.enterprise_user_accounts a ON a.identity_id=i.id
 JOIN tenancy.memberships m ON m.account_id=a.id AND m.organization_id=a.organization_id
 JOIN tenancy.organizations o ON o.id=a.organization_id
 WHERE i.id=authz.ctx('app.identity_id') AND a.id=authz.ctx('app.account_id')
 AND o.id=authz.ctx('app.organization_id') AND i.status='Active'
 AND a.status='Active' AND m.status='Active' AND o.status='Active'
 AND (NOT write_required OR m.role IN('Owner','Admin'))) $$;

CREATE FUNCTION authz.validate_integrity() RETURNS trigger LANGUAGE plpgsql VOLATILE SECURITY DEFINER
SET search_path=pg_catalog,pg_temp AS $$
DECLARE org uuid; obj record; ostate text;
BEGIN
 IF TG_TABLE_NAME='organizations' THEN org:=NEW.id; ELSE org:=NEW.organization_id; END IF;
 IF org IS NULL OR org IS DISTINCT FROM authz.ctx('app.integrity_org')
 OR current_setting('app.integrity_tx',true) IS DISTINCT FROM pg_current_xact_id()::text
 OR nullif(current_setting('app.operation_id',true),'') IS NULL
 THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Integrity.ContextInvalid'; END IF;
 SELECT status INTO ostate FROM tenancy.organizations WHERE id=org;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P1002',MESSAGE='Integrity.AnchorMissing'; END IF;
 -- Deliberately no row/advisory locks: writers already hold the org lock.
 FOR obj IN SELECT a.id,a.status FROM identity.enterprise_user_accounts a WHERE a.organization_id=org LOOP
  IF obj.status='Active' AND (SELECT count(*) FROM tenancy.memberships m WHERE m.organization_id=org AND m.account_id=obj.id AND m.status='Active')<>1
  THEN RAISE EXCEPTION USING ERRCODE='P1003',MESSAGE='Integrity.AccountMembership'; END IF;
  IF obj.status<>'Active' AND EXISTS(SELECT FROM tenancy.memberships m WHERE m.organization_id=org AND m.account_id=obj.id AND m.status='Active')
  THEN RAISE EXCEPTION USING ERRCODE='P1003',MESSAGE='Integrity.AccountMembership'; END IF;
 END LOOP;
 IF EXISTS(SELECT FROM tenancy.memberships m LEFT JOIN identity.enterprise_user_accounts a ON a.id=m.account_id AND a.organization_id=m.organization_id
 WHERE m.organization_id=org AND m.status='Active' AND (a.id IS NULL OR a.status<>'Active'))
 THEN RAISE EXCEPTION USING ERRCODE='P1003',MESSAGE='Integrity.AccountMembership'; END IF;
 IF ostate='Active' AND NOT EXISTS(SELECT FROM tenancy.memberships m
 JOIN identity.enterprise_user_accounts a ON a.id=m.account_id AND a.organization_id=m.organization_id
 WHERE m.organization_id=org AND m.role='Owner' AND m.status='Active' AND a.status='Active')
 THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Integrity.OwnerMissing'; END IF;
 IF TG_TABLE_NAME='memberships' THEN
  IF NOT EXISTS(SELECT FROM identity.enterprise_user_accounts WHERE id=NEW.account_id AND organization_id=org)
  THEN RAISE EXCEPTION USING ERRCODE='P1002',MESSAGE='Integrity.AnchorMissing'; END IF;
 END IF;
 RETURN NULL;
END $$;
-- INVOKER: structural rules apply even to fixture/operator writes; authorization
-- below is additionally enforced for the implemented tenant replacement writer.
CREATE FUNCTION authz.guard_invitation() RETURNS trigger LANGUAGE plpgsql
SET search_path=pg_catalog,pg_temp AS $$
DECLARE predecessor record;
BEGIN
 IF TG_OP='INSERT' THEN
  IF NEW.status<>'Pending' OR NEW.accepted_account_id IS NOT NULL OR NEW.successor_id IS NOT NULL OR NEW.version<>1
  THEN RAISE EXCEPTION USING ERRCODE='P1010',MESSAGE='Invitation.InvalidInsert'; END IF;
  IF current_user='exec_tenant' THEN
   IF NOT authz.own_tenant_allowed(true) OR NEW.organization_id IS DISTINCT FROM authz.ctx('app.organization_id')
    OR NEW.recipient_identity_id IS DISTINCT FROM authz.ctx('app.recipient_id')
    OR NOT EXISTS(SELECT FROM tenancy.memberships WHERE id=NEW.issuer_membership_id
      AND account_id=authz.ctx('app.account_id') AND organization_id=NEW.organization_id AND status='Active' AND role IN('Owner','Admin'))
   THEN RAISE EXCEPTION USING ERRCODE='P1011',MESSAGE='Invitation.IssuerDenied'; END IF;
   IF EXISTS(SELECT FROM identity.identity_consumptions WHERE identity_id=NEW.recipient_identity_id)
    OR NOT EXISTS(SELECT FROM identity.identities WHERE id=NEW.recipient_identity_id AND status='Active')
   THEN RAISE EXCEPTION USING ERRCODE='P1012',MESSAGE='Invitation.RecipientIneligible'; END IF;
   -- No initial-issuance SQL port exists. This is not a table-wide NOT NULL
   -- requirement: initial privileged fixtures retain predecessor NULL.
   IF NEW.predecessor_id IS NULL THEN
    RAISE EXCEPTION USING ERRCODE='P1013',MESSAGE='Invitation.InitialIssueUnavailable'; END IF;
  END IF;
  IF NEW.predecessor_id IS NOT NULL THEN
   SELECT id,organization_id,recipient_identity_id,status,successor_id INTO predecessor FROM tenancy.invitations WHERE id=NEW.predecessor_id;
   IF NOT FOUND OR predecessor.organization_id<>NEW.organization_id OR predecessor.recipient_identity_id<>NEW.recipient_identity_id
    OR predecessor.status<>'Replaced' OR predecessor.successor_id IS NOT NULL OR NEW.predecessor_id=NEW.id
   THEN RAISE EXCEPTION USING ERRCODE='P1014',MESSAGE='Invitation.InvalidChain'; END IF;
  END IF;
 ELSE
  IF (to_jsonb(OLD)-ARRAY['status','version','accepted_account_id','successor_id']) IS DISTINCT FROM
     (to_jsonb(NEW)-ARRAY['status','version','accepted_account_id','successor_id'])
  THEN RAISE EXCEPTION USING ERRCODE='P1014',MESSAGE='Invitation.Immutable'; END IF;
  IF OLD.status IS DISTINCT FROM NEW.status AND NOT
    ((OLD.status='Pending' AND NEW.status IN('Accepted','Expired','Cancelled','Replaced')) OR (OLD.status='Expired' AND NEW.status='Replaced'))
  THEN RAISE EXCEPTION USING ERRCODE='P1014',MESSAGE='Invitation.InvalidTransition'; END IF;
  IF NEW.accepted_account_id IS DISTINCT FROM OLD.accepted_account_id THEN
   IF OLD.status<>'Pending' OR NEW.status<>'Accepted' OR OLD.accepted_account_id IS NOT NULL
    OR NOT EXISTS(SELECT FROM identity.enterprise_user_accounts WHERE id=NEW.accepted_account_id AND identity_id=NEW.recipient_identity_id AND organization_id=NEW.organization_id AND status='Active')
   THEN RAISE EXCEPTION USING ERRCODE='P1014',MESSAGE='Invitation.InvalidAcceptance'; END IF;
  END IF;
  IF NEW.successor_id IS DISTINCT FROM OLD.successor_id THEN
   IF OLD.successor_id IS NOT NULL OR OLD.status<>'Replaced' OR NEW.status<>'Replaced'
    OR NOT EXISTS(SELECT FROM tenancy.invitations WHERE id=NEW.successor_id AND predecessor_id=NEW.id AND organization_id=NEW.organization_id AND recipient_identity_id=NEW.recipient_identity_id AND status='Pending')
   THEN RAISE EXCEPTION USING ERRCODE='P1014',MESSAGE='Invitation.InvalidChain'; END IF;
  END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE FUNCTION authz.validate_invitation_chain() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,pg_temp AS $$
DECLARE item record;
BEGIN
 IF NEW.organization_id IS DISTINCT FROM authz.ctx('app.integrity_org')
 OR current_setting('app.integrity_tx',true) IS DISTINCT FROM pg_current_xact_id()::text
 THEN RAISE EXCEPTION USING ERRCODE='P1014',MESSAGE='Invitation.ChainContextInvalid'; END IF;
 SELECT id,organization_id,recipient_identity_id,status,predecessor_id,successor_id INTO item FROM tenancy.invitations WHERE id=NEW.id;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P1014',MESSAGE='Invitation.ChainAnchorMissing'; END IF;
 IF (item.status='Replaced' AND item.successor_id IS NULL)
 OR (item.predecessor_id IS NOT NULL AND NOT EXISTS(SELECT FROM tenancy.invitations WHERE id=item.predecessor_id AND successor_id=item.id AND status='Replaced' AND organization_id=item.organization_id AND recipient_identity_id=item.recipient_identity_id))
 THEN RAISE EXCEPTION USING ERRCODE='P1014',MESSAGE='Invitation.IncompleteChain'; END IF;
 RETURN NULL;
END $$;
CREATE TRIGGER guard_invitation BEFORE INSERT OR UPDATE ON tenancy.invitations FOR EACH ROW EXECUTE FUNCTION authz.guard_invitation();
CREATE CONSTRAINT TRIGGER validate_invitation_chain AFTER INSERT OR UPDATE ON tenancy.invitations DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION authz.validate_invitation_chain();

CREATE FUNCTION authz.guard_immutable() RETURNS trigger LANGUAGE plpgsql
SET search_path=pg_catalog,pg_temp AS $$
BEGIN
 IF TG_OP='DELETE' OR TG_TABLE_NAME='identity_consumptions' THEN
 RAISE EXCEPTION USING ERRCODE='P1005',MESSAGE='Integrity.Immutable'; END IF;
 IF TG_TABLE_NAME IN('enterprise_user_accounts','memberships') THEN
  IF OLD.status IS DISTINCT FROM NEW.status AND NOT (OLD.status='Active' AND NEW.status='Revoked')
  THEN RAISE EXCEPTION USING ERRCODE='P1006',MESSAGE='Integrity.TransitionDenied'; END IF;
 END IF;
 IF TG_TABLE_NAME='memberships' THEN
  IF OLD.role='Owner' AND OLD.status='Active' AND NEW.status='Revoked'
  THEN RAISE EXCEPTION USING ERRCODE='P1007',MESSAGE='Integrity.OwnerRevocationDenied'; END IF;
  IF OLD.role IS DISTINCT FROM NEW.role
  THEN RAISE EXCEPTION USING ERRCODE='P1006',MESSAGE='Integrity.RoleImmutable'; END IF;
 END IF;
 IF (to_jsonb(OLD)-ARRAY['status','version','name','role']) IS DISTINCT FROM
    (to_jsonb(NEW)-ARRAY['status','version','name','role'])
 THEN RAISE EXCEPTION USING ERRCODE='P1005',MESSAGE='Integrity.Immutable'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER guard_accounts BEFORE UPDATE OR DELETE ON identity.enterprise_user_accounts FOR EACH ROW EXECUTE FUNCTION authz.guard_immutable();
CREATE TRIGGER guard_members BEFORE UPDATE OR DELETE ON tenancy.memberships FOR EACH ROW EXECUTE FUNCTION authz.guard_immutable();
CREATE TRIGGER guard_organizations BEFORE UPDATE OR DELETE ON tenancy.organizations FOR EACH ROW EXECUTE FUNCTION authz.guard_immutable();
CREATE TRIGGER guard_consumptions BEFORE UPDATE OR DELETE ON identity.identity_consumptions FOR EACH ROW EXECUTE FUNCTION authz.guard_immutable();
CREATE TRIGGER guard_links BEFORE UPDATE OR DELETE ON identity.identity_links FOR EACH ROW EXECUTE FUNCTION authz.guard_immutable();
CREATE CONSTRAINT TRIGGER validate_accounts AFTER INSERT OR UPDATE ON identity.enterprise_user_accounts DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION authz.validate_integrity();
CREATE CONSTRAINT TRIGGER validate_members AFTER INSERT OR UPDATE ON tenancy.memberships DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION authz.validate_integrity();
CREATE CONSTRAINT TRIGGER validate_org AFTER INSERT OR UPDATE ON tenancy.organizations DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION authz.validate_integrity();
RESET ROLE;

ALTER FUNCTION authz.own_tenant_allowed(boolean) OWNER TO exec_context;
ALTER FUNCTION authz.validate_integrity() OWNER TO exec_integrity;
ALTER FUNCTION authz.guard_immutable() OWNER TO exec_integrity;
ALTER FUNCTION authz.guard_invitation() OWNER TO exec_integrity;
ALTER FUNCTION authz.validate_invitation_chain() OWNER TO exec_integrity;
GRANT USAGE ON SCHEMA identity,tenancy,sandbox,audit,operation,authz TO
 exec_identity,exec_tenant,exec_bootstrap,exec_context,exec_integrity,core_identity,core_tenant,core_platform,g1_probe;
GRANT EXECUTE ON FUNCTION authz.ctx(text) TO exec_identity,exec_tenant,exec_bootstrap,exec_context,exec_integrity,core_identity,core_tenant,core_platform,g1_probe;
GRANT EXECUTE ON FUNCTION authz.own_tenant_allowed(boolean) TO core_tenant,exec_tenant;

GRANT SELECT(id,status) ON identity.identities TO exec_context;
GRANT SELECT(id,identity_id,organization_id,status) ON identity.enterprise_user_accounts TO exec_context;
GRANT SELECT(account_id,organization_id,status,role) ON tenancy.memberships TO exec_context;
GRANT SELECT(id,status) ON tenancy.organizations TO exec_context;
GRANT SELECT(id,status) ON tenancy.organizations TO exec_integrity;
GRANT SELECT(id,organization_id,status) ON identity.enterprise_user_accounts TO exec_integrity;
GRANT SELECT(account_id,organization_id,status,role) ON tenancy.memberships TO exec_integrity;
GRANT SELECT ON identity.recipient_references TO exec_bootstrap;
GRANT SELECT ON identity.identities,identity.identity_consumptions,tenancy.organizations,operation.idempotency_records TO exec_bootstrap;
GRANT INSERT ON tenancy.organizations,identity.identity_consumptions,identity.enterprise_user_accounts,tenancy.memberships,audit.events,operation.idempotency_records TO exec_bootstrap;
GRANT UPDATE(version) ON identity.identities TO exec_bootstrap;
GRANT SELECT ON identity.identities,identity.enterprise_user_accounts,identity.identity_consumptions,tenancy.memberships,tenancy.organizations,tenancy.invitations,tenancy.invitation_slots TO exec_identity;
GRANT INSERT ON identity.identity_consumptions,identity.enterprise_user_accounts,tenancy.memberships,audit.events,tenancy.invitation_slots TO exec_identity;
GRANT UPDATE(version) ON identity.identities,tenancy.organizations,tenancy.invitation_slots TO exec_identity;
GRANT UPDATE(status,accepted_account_id,version) ON tenancy.invitations TO exec_identity;
GRANT SELECT ON identity.identities,identity.enterprise_user_accounts,tenancy.memberships,tenancy.organizations,sandbox.demo_records,operation.idempotency_records TO exec_tenant;
GRANT UPDATE(version) ON identity.identities,tenancy.organizations TO exec_tenant;
GRANT UPDATE(status,version) ON identity.enterprise_user_accounts TO exec_tenant;
GRANT UPDATE(status,version) ON tenancy.memberships TO exec_tenant;
GRANT INSERT ON audit.events,operation.idempotency_records TO exec_tenant;
GRANT UPDATE(title,version) ON sandbox.demo_records TO exec_tenant;
GRANT SELECT ON sandbox.demo_records TO core_tenant;
-- Dedicated test-only writer. No role memberships, bypass, DDL or runtime availability.
GRANT SELECT,INSERT ON identity.identity_consumptions,identity.enterprise_user_accounts,tenancy.memberships,tenancy.organizations TO g1_probe;
GRANT UPDATE(status,version) ON identity.enterprise_user_accounts,tenancy.organizations TO g1_probe;
GRANT UPDATE(status,role,version) ON tenancy.memberships TO g1_probe;

DO $$ DECLARE t record; r text;
BEGIN
 FOR t IN SELECT c.oid::regclass AS obj,n.nspname,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE n.nspname IN('identity','tenancy','sandbox','audit','operation') AND c.relkind='r' LOOP
 EXECUTE format('ALTER TABLE %s ENABLE ROW LEVEL SECURITY',t.obj);
 EXECUTE format('ALTER TABLE %s FORCE ROW LEVEL SECURITY',t.obj);
 END LOOP;
END $$;
-- Base policies for technical read-only contexts do not call the helper.
CREATE POLICY ctx_identity ON identity.identities TO exec_context USING(id=authz.ctx('app.identity_id'));
CREATE POLICY ctx_account ON identity.enterprise_user_accounts TO exec_context USING(identity_id=authz.ctx('app.identity_id'));
CREATE POLICY ctx_member ON tenancy.memberships TO exec_context USING(account_id=authz.ctx('app.account_id'));
CREATE POLICY ctx_org ON tenancy.organizations TO exec_context USING(id=authz.ctx('app.organization_id'));
CREATE POLICY integrity_org ON tenancy.organizations TO exec_integrity USING(id=authz.ctx('app.integrity_org'));
CREATE POLICY integrity_account ON identity.enterprise_user_accounts TO exec_integrity USING(organization_id=authz.ctx('app.integrity_org'));
CREATE POLICY integrity_member ON tenancy.memberships TO exec_integrity USING(organization_id=authz.ctx('app.integrity_org'));
CREATE POLICY integrity_invitation ON tenancy.invitations TO exec_integrity USING(organization_id=authz.ctx('app.integrity_org'));
CREATE POLICY integrity_consumption ON identity.identity_consumptions TO exec_integrity USING(organization_id=authz.ctx('app.integrity_org'));

CREATE POLICY bootstrap_identity ON identity.identities TO exec_bootstrap USING(id IN(authz.ctx('app.identity_id'),authz.ctx('app.recipient_id'))) WITH CHECK(false);
CREATE POLICY bootstrap_reference ON identity.recipient_references FOR SELECT TO exec_bootstrap USING(identity_id=authz.ctx('app.recipient_id'));
CREATE POLICY bootstrap_org ON tenancy.organizations TO exec_bootstrap USING(id=authz.ctx('app.integrity_org')) WITH CHECK(id=authz.ctx('app.integrity_org'));
CREATE POLICY bootstrap_hist ON identity.identity_consumptions TO exec_bootstrap USING(identity_id=authz.ctx('app.recipient_id')) WITH CHECK(identity_id=authz.ctx('app.recipient_id') AND organization_id=authz.ctx('app.integrity_org'));
CREATE POLICY bootstrap_account ON identity.enterprise_user_accounts TO exec_bootstrap USING(identity_id=authz.ctx('app.recipient_id')) WITH CHECK(identity_id=authz.ctx('app.recipient_id') AND organization_id=authz.ctx('app.integrity_org'));
CREATE POLICY bootstrap_member ON tenancy.memberships TO exec_bootstrap USING(organization_id=authz.ctx('app.integrity_org')) WITH CHECK(organization_id=authz.ctx('app.integrity_org') AND role='Owner');
CREATE POLICY bootstrap_audit ON audit.events FOR INSERT TO exec_bootstrap WITH CHECK(organization_id=authz.ctx('app.integrity_org') AND actor_id=authz.ctx('app.identity_id'));
CREATE POLICY bootstrap_receipt ON operation.idempotency_records TO exec_bootstrap USING(actor_id=authz.ctx('app.identity_id') AND scope_key='Platform') WITH CHECK(actor_id=authz.ctx('app.identity_id') AND scope_key='Platform');

CREATE POLICY identity_self ON identity.identities TO exec_identity USING(id=authz.ctx('app.identity_id')) WITH CHECK(false);
CREATE POLICY identity_org ON tenancy.organizations TO exec_identity USING(id=authz.ctx('app.organization_id')) WITH CHECK(false);
CREATE POLICY identity_hist ON identity.identity_consumptions TO exec_identity USING(identity_id=authz.ctx('app.identity_id')) WITH CHECK(identity_id=authz.ctx('app.identity_id') AND organization_id=authz.ctx('app.organization_id'));
CREATE POLICY identity_account ON identity.enterprise_user_accounts TO exec_identity USING(identity_id=authz.ctx('app.identity_id')) WITH CHECK(identity_id=authz.ctx('app.identity_id') AND organization_id=authz.ctx('app.organization_id'));
CREATE POLICY identity_member ON tenancy.memberships TO exec_identity USING(organization_id=authz.ctx('app.organization_id')) WITH CHECK(organization_id=authz.ctx('app.organization_id') AND role='Member');
CREATE POLICY identity_invite ON tenancy.invitations TO exec_identity USING(recipient_identity_id=authz.ctx('app.identity_id')) WITH CHECK(recipient_identity_id=authz.ctx('app.identity_id') AND status='Accepted');
CREATE POLICY identity_slot ON tenancy.invitation_slots TO exec_identity USING(recipient_identity_id=authz.ctx('app.identity_id')) WITH CHECK(recipient_identity_id=authz.ctx('app.identity_id') AND organization_id=authz.ctx('app.organization_id'));
CREATE POLICY identity_audit ON audit.events FOR INSERT TO exec_identity WITH CHECK(actor_id=authz.ctx('app.identity_id') AND organization_id=authz.ctx('app.organization_id'));

CREATE POLICY tenant_identity ON identity.identities TO exec_tenant USING(id IN(authz.ctx('app.identity_id'),authz.ctx('app.recipient_id'))) WITH CHECK(false);
CREATE POLICY tenant_org ON tenancy.organizations TO exec_tenant USING(id=authz.ctx('app.organization_id')) WITH CHECK(false);
CREATE POLICY tenant_account ON identity.enterprise_user_accounts TO exec_tenant USING(organization_id=authz.ctx('app.organization_id')) WITH CHECK(organization_id=authz.ctx('app.organization_id'));
CREATE POLICY tenant_member ON tenancy.memberships TO exec_tenant USING(organization_id=authz.ctx('app.organization_id')) WITH CHECK(organization_id=authz.ctx('app.organization_id'));
CREATE POLICY tenant_demo ON sandbox.demo_records TO exec_tenant USING(organization_id=authz.ctx('app.organization_id')) WITH CHECK(organization_id=authz.ctx('app.organization_id'));
CREATE POLICY tenant_read ON sandbox.demo_records FOR SELECT TO core_tenant USING(organization_id=authz.ctx('app.organization_id') AND authz.own_tenant_allowed(false));
CREATE POLICY tenant_audit ON audit.events FOR INSERT TO exec_tenant WITH CHECK(actor_id=authz.ctx('app.identity_id') AND organization_id=authz.ctx('app.organization_id'));
CREATE POLICY tenant_receipt ON operation.idempotency_records TO exec_tenant USING(actor_id=authz.ctx('app.identity_id') AND scope_key=authz.ctx('app.organization_id')::text) WITH CHECK(actor_id=authz.ctx('app.identity_id') AND scope_key=authz.ctx('app.organization_id')::text);

DO $$ DECLARE t record; BEGIN
 FOR t IN SELECT c.oid::regclass obj,n.nspname,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE (n.nspname='identity' AND c.relname IN('identity_consumptions','enterprise_user_accounts'))
 OR (n.nspname='tenancy' AND c.relname IN('organizations','memberships')) LOOP
 EXECUTE format('CREATE POLICY probe_org ON %s TO g1_probe USING (%I=authz.ctx(''app.integrity_org'')) WITH CHECK (%I=authz.ctx(''app.integrity_org''))',
 t.obj,CASE WHEN t.relname='organizations' THEN 'id' ELSE 'organization_id' END,CASE WHEN t.relname='organizations' THEN 'id' ELSE 'organization_id' END);
 END LOOP;
END $$;
