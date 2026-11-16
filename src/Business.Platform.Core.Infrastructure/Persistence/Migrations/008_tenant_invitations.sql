-- E2-03 product migration. No runtime logins, real secrets, automatic provisioning or purge.
SET LOCAL ROLE bpc_e1_ddl;
CREATE UNIQUE INDEX owner_reference_identity ON identity.owner_references(reference_hash,identity_id);
ALTER TABLE identity.identity_consumptions DROP CONSTRAINT identity_consumptions_origin_check;
ALTER TABLE identity.identity_consumptions ADD CONSTRAINT identity_consumptions_origin_check CHECK(origin IN ('PlatformBootstrap','Invitation'));
ALTER TABLE bpc_security.events DROP CONSTRAINT events_kind_check;
ALTER TABLE bpc_security.events ADD CONSTRAINT events_kind_check CHECK(kind IN ('OrganizationBootstrapped','MembershipRevoked','MembershipRoleChanged','InvitationIssued','InvitationAccepted','InvitationCancelled','InvitationExpired','InvitationReplaced'));
CREATE TABLE tenancy.invitation_slots(organization_id uuid NOT NULL REFERENCES tenancy.organizations,recipient_identity_id uuid NOT NULL REFERENCES identity.identities,version bigint NOT NULL DEFAULT 1,PRIMARY KEY(organization_id,recipient_identity_id));
CREATE TABLE tenancy.invitations(
 invitation_id uuid PRIMARY KEY,organization_id uuid NOT NULL REFERENCES tenancy.organizations,recipient_identity_id uuid NOT NULL REFERENCES identity.identities,
 issuer_membership_id uuid NOT NULL,reference_hash bytea NOT NULL,secret_hash bytea NOT NULL UNIQUE CHECK(octet_length(secret_hash)=32),
 status text NOT NULL CHECK(status IN ('Pending','Accepted','Expired','Cancelled','Replaced')),version bigint NOT NULL DEFAULT 1 CHECK(version>0),
 created_at timestamptz NOT NULL,expires_at timestamptz NOT NULL CHECK(expires_at=created_at+interval '24 hours'),
 accepted_at timestamptz,account_id uuid,membership_id uuid,predecessor_id uuid UNIQUE,successor_id uuid UNIQUE,
 UNIQUE(organization_id,invitation_id),FOREIGN KEY(organization_id,issuer_membership_id) REFERENCES tenancy.memberships(organization_id,membership_id),
 FOREIGN KEY(reference_hash,recipient_identity_id) REFERENCES identity.owner_references(reference_hash,identity_id),
 FOREIGN KEY(recipient_identity_id,organization_id,account_id) REFERENCES identity.identity_consumptions(identity_id,organization_id,account_id) DEFERRABLE INITIALLY DEFERRED,
 FOREIGN KEY(organization_id,membership_id) REFERENCES tenancy.memberships(organization_id,membership_id) DEFERRABLE INITIALLY DEFERRED,
 FOREIGN KEY(organization_id,predecessor_id) REFERENCES tenancy.invitations(organization_id,invitation_id) DEFERRABLE INITIALLY DEFERRED,
 FOREIGN KEY(organization_id,successor_id) REFERENCES tenancy.invitations(organization_id,invitation_id) DEFERRABLE INITIALLY DEFERRED,
 CHECK((status='Accepted' AND accepted_at IS NOT NULL AND account_id IS NOT NULL AND membership_id IS NOT NULL AND accepted_at<expires_at) OR
       (status<>'Accepted' AND accepted_at IS NULL AND account_id IS NULL AND membership_id IS NULL)),
 CHECK((status='Replaced')=(successor_id IS NOT NULL)),CHECK(predecessor_id IS DISTINCT FROM invitation_id AND successor_id IS DISTINCT FROM invitation_id));
CREATE UNIQUE INDEX one_pending_invitation ON tenancy.invitations(organization_id,recipient_identity_id) WHERE status='Pending';
CREATE UNIQUE INDEX one_accepted_identity ON tenancy.invitations(recipient_identity_id) WHERE status='Accepted';
CREATE INDEX invitation_recipient ON tenancy.invitations(recipient_identity_id,invitation_id);
CREATE INDEX invitation_page ON tenancy.invitations(organization_id,created_at DESC,invitation_id DESC);
ALTER TABLE bpc_security.events ALTER COLUMN target_account_id DROP NOT NULL;
ALTER TABLE bpc_security.events ADD COLUMN invitation_id uuid;
ALTER TABLE bpc_security.events ADD CONSTRAINT event_invitation_fk FOREIGN KEY(organization_id,invitation_id) REFERENCES tenancy.invitations(organization_id,invitation_id) DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE bpc_security.events ADD CONSTRAINT event_target_shape CHECK(
 (kind IN ('InvitationIssued','InvitationCancelled','InvitationExpired','InvitationReplaced') AND target_account_id IS NULL AND invitation_id IS NOT NULL)
 OR (kind='InvitationAccepted' AND target_account_id IS NOT NULL AND invitation_id IS NOT NULL)
 OR (kind IN ('OrganizationBootstrapped','MembershipRevoked','MembershipRoleChanged') AND target_account_id IS NOT NULL AND invitation_id IS NULL));
CREATE TABLE bpc_security.invitation_receipts(actor_identity_id uuid NOT NULL,organization_id uuid NOT NULL,action text NOT NULL CHECK(action IN ('Issue','Replace')),
 operation_id uuid NOT NULL,request_hash bytea NOT NULL CHECK(octet_length(request_hash)=32),invitation_id uuid NOT NULL,
 created_at timestamptz NOT NULL,expires_at timestamptz NOT NULL CHECK(expires_at=created_at+interval '48 hours'),
 PRIMARY KEY(actor_identity_id,organization_id,action,operation_id),FOREIGN KEY(organization_id,invitation_id) REFERENCES tenancy.invitations(organization_id,invitation_id));
RESET ROLE;
DO $$ DECLARE t text; BEGIN FOREACH t IN ARRAY ARRAY['tenancy.invitation_slots','tenancy.invitations','bpc_security.invitation_receipts'] LOOP EXECUTE format('ALTER TABLE %s ENABLE ROW LEVEL SECURITY',t); EXECUTE format('ALTER TABLE %s FORCE ROW LEVEL SECURITY',t); END LOOP; END $$;
CREATE POLICY invitation_tenant_select ON tenancy.invitations FOR SELECT TO bpc_e1_tenant_exec USING(organization_id=bpc_security.ctx('app.organization_id'));
CREATE POLICY invitation_tenant_insert ON tenancy.invitations FOR INSERT TO bpc_e1_tenant_exec WITH CHECK(organization_id=bpc_security.ctx('app.organization_id') AND status='Pending' AND version=1 AND account_id IS NULL);
CREATE POLICY invitation_tenant_update ON tenancy.invitations FOR UPDATE TO bpc_e1_tenant_exec USING(organization_id=bpc_security.ctx('app.organization_id')) WITH CHECK(organization_id=bpc_security.ctx('app.organization_id') AND status IN ('Pending','Expired','Cancelled','Replaced'));
CREATE POLICY invitation_recipient_select ON tenancy.invitations FOR SELECT TO bpc_e1_identity_exec USING(recipient_identity_id=bpc_security.ctx('app.identity_id'));
CREATE POLICY invitation_recipient_update ON tenancy.invitations FOR UPDATE TO bpc_e1_identity_exec USING(recipient_identity_id=bpc_security.ctx('app.identity_id') AND organization_id=bpc_security.ctx('app.organization_id')) WITH CHECK(recipient_identity_id=bpc_security.ctx('app.identity_id') AND organization_id=bpc_security.ctx('app.organization_id') AND status='Accepted');
CREATE POLICY slot_tenant ON tenancy.invitation_slots TO bpc_e1_tenant_exec USING(organization_id=bpc_security.ctx('app.organization_id')) WITH CHECK(organization_id=bpc_security.ctx('app.organization_id'));
CREATE POLICY slot_recipient ON tenancy.invitation_slots TO bpc_e1_identity_exec USING(recipient_identity_id=bpc_security.ctx('app.identity_id') AND organization_id=bpc_security.ctx('app.organization_id'));
CREATE POLICY invitation_receipt_tenant ON bpc_security.invitation_receipts TO bpc_e1_tenant_exec USING(actor_identity_id=bpc_security.ctx('app.identity_id') AND organization_id=bpc_security.ctx('app.organization_id')) WITH CHECK(actor_identity_id=bpc_security.ctx('app.identity_id') AND organization_id=bpc_security.ctx('app.organization_id'));
GRANT SELECT(name) ON tenancy.organizations TO bpc_e1_tenant_exec;
GRANT SELECT ON tenancy.invitations TO bpc_e1_tenant_exec,bpc_e1_identity_exec,bpc_e1_integrity_exec;
GRANT INSERT(invitation_id,organization_id,recipient_identity_id,issuer_membership_id,reference_hash,secret_hash,status,created_at,expires_at,predecessor_id) ON tenancy.invitations TO bpc_e1_tenant_exec;
GRANT UPDATE(status,version,successor_id) ON tenancy.invitations TO bpc_e1_tenant_exec;
GRANT UPDATE(status,version,accepted_at,account_id,membership_id) ON tenancy.invitations TO bpc_e1_identity_exec;
GRANT SELECT,INSERT ON tenancy.invitation_slots TO bpc_e1_tenant_exec;
GRANT SELECT,UPDATE(version) ON tenancy.invitation_slots TO bpc_e1_identity_exec;
GRANT UPDATE(version) ON tenancy.invitation_slots TO bpc_e1_tenant_exec;
GRANT SELECT,INSERT ON bpc_security.invitation_receipts TO bpc_e1_tenant_exec;
GRANT USAGE ON SCHEMA bpc_crypto TO bpc_e1_tenant_exec;
GRANT EXECUTE ON FUNCTION bpc_crypto.digest(bytea,text) TO bpc_e1_tenant_exec;
-- Internal helper shares existing bootstrap executor, never exposed to runtime.
CREATE FUNCTION bpc_security.invitation_identity_locks(p_actor uuid,p_recipient uuid) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE id uuid; st text; saved text:=current_setting('app.identity_id',true); eligible boolean;
BEGIN
 FOR id IN SELECT DISTINCT x FROM unnest(ARRAY[p_actor,p_recipient]) x WHERE x IS NOT NULL ORDER BY x LOOP
  PERFORM set_config('app.identity_id',id::text,true); SELECT status INTO st FROM identity.identities WHERE identity_id=id FOR UPDATE;
  IF st IS DISTINCT FROM 'Active' THEN RAISE EXCEPTION USING ERRCODE='P1804',MESSAGE='Invitation unavailable'; END IF;
 END LOOP;
 PERFORM set_config('app.identity_id',p_recipient::text,true);
 eligible:=NOT EXISTS(SELECT FROM identity.identity_consumptions WHERE identity_id=p_recipient);
 PERFORM set_config('app.identity_id',coalesce(saved,''),true); RETURN eligible;
END $$;
ALTER FUNCTION bpc_security.invitation_identity_locks(uuid,uuid) OWNER TO bpc_e1_boot_exec;
REVOKE ALL ON FUNCTION bpc_security.invitation_identity_locks(uuid,uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION bpc_security.invitation_identity_locks(uuid,uuid) TO bpc_e1_tenant_exec,bpc_e1_identity_exec;
-- C02 validity is checked with wall clock AFTER lock waits, not outer statement time.
CREATE FUNCTION bpc_security.invitation_reference_current(p_user uuid,p_hash bytea) RETURNS boolean
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE saved text:=current_setting('app.identity_id',true); valid boolean;
BEGIN
 PERFORM set_config('app.identity_id',p_user::text,true);
 SELECT EXISTS(SELECT FROM identity.owner_references r JOIN identity.identities i ON i.identity_id=r.identity_id
 WHERE r.identity_id=p_user AND r.reference_hash=p_hash AND r.expires_at>clock_timestamp() AND i.status='Active'
 AND NOT EXISTS(SELECT FROM identity.identity_consumptions c WHERE c.identity_id=p_user)) INTO valid;
 PERFORM set_config('app.identity_id',coalesce(saved,''),true); RETURN valid;
END $$;
ALTER FUNCTION bpc_security.invitation_reference_current(uuid,bytea) OWNER TO bpc_e1_boot_exec;
REVOKE ALL ON FUNCTION bpc_security.invitation_reference_current(uuid,bytea) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION bpc_security.invitation_reference_current(uuid,bytea) TO bpc_e1_tenant_exec;
CREATE FUNCTION bpc_security.invitation_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,pg_temp AS $$
DECLARE saved text:=current_setting('app.identity_id',true); actor_ctx jsonb;
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Protected invitation'; END IF;
 IF TG_OP='INSERT' THEN
  IF current_user<>'bpc_e1_tenant_exec' OR NEW.status<>'Pending' OR NEW.version<>1 OR NEW.created_at>clock_timestamp() OR NEW.expires_at<=clock_timestamp() THEN
   RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Protected invitation'; END IF;
  actor_ctx:=bpc_security.resolve_context(current_setting('app.issuer',true),current_setting('app.subject',true));
  IF actor_ctx->>'state' IS DISTINCT FROM 'Linked' OR actor_ctx->>'role' IS NULL OR actor_ctx->>'role' NOT IN ('Owner','Admin') OR (actor_ctx->>'organizationId')::uuid IS DISTINCT FROM NEW.organization_id OR (actor_ctx->>'membershipId')::uuid IS DISTINCT FROM NEW.issuer_membership_id THEN
   RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Protected invitation'; END IF;
  IF NEW.predecessor_id IS NULL THEN
   IF NOT bpc_security.invitation_reference_current(NEW.recipient_identity_id,NEW.reference_hash) THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Recipient unavailable'; END IF;
   PERFORM set_config('app.identity_id',coalesce(saved,''),true);
  ELSE
   IF NOT EXISTS(SELECT FROM tenancy.invitations p WHERE p.invitation_id=NEW.predecessor_id AND p.organization_id=NEW.organization_id AND p.recipient_identity_id=NEW.recipient_identity_id AND p.reference_hash=NEW.reference_hash AND p.status='Replaced' AND p.successor_id=NEW.invitation_id) THEN
    RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Protected invitation'; END IF;
  END IF;
 ELSE
  IF (to_jsonb(NEW)-ARRAY['status','version','accepted_at','account_id','membership_id','successor_id']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['status','version','accepted_at','account_id','membership_id','successor_id'])
   OR NEW.version<>OLD.version+1 OR OLD.status NOT IN ('Pending','Expired')
   OR current_user='bpc_e1_tenant_exec' AND NEW.status NOT IN ('Expired','Cancelled','Replaced')
   OR current_user='bpc_e1_identity_exec' AND (OLD.status<>'Pending' OR NEW.status<>'Accepted')
   OR current_user NOT IN ('bpc_e1_tenant_exec','bpc_e1_identity_exec') THEN
   RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Protected invitation'; END IF;
 END IF; RETURN NEW;
END $$;
ALTER FUNCTION bpc_security.invitation_guard() OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.invitation_guard() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION bpc_security.invitation_guard() TO bpc_e1_identity_exec;
CREATE TRIGGER invitation_guard BEFORE INSERT OR UPDATE OR DELETE ON tenancy.invitations FOR EACH ROW EXECUTE FUNCTION bpc_security.invitation_guard();
-- Recipient inserts only its own account, Member and permanent Invitation consumption.
GRANT INSERT(identity_id,organization_id,account_id,origin) ON identity.identity_consumptions TO bpc_e1_identity_exec;
GRANT INSERT(account_id,identity_id,organization_id,status) ON identity.enterprise_user_accounts TO bpc_e1_identity_exec;
GRANT UPDATE(version) ON tenancy.organizations TO bpc_e1_identity_exec;
GRANT INSERT(membership_id,organization_id,account_id,status,role) ON tenancy.memberships TO bpc_e1_identity_exec;
GRANT INSERT(event_id,operation_id,organization_id,actor_id,target_account_id,kind,invitation_id) ON bpc_security.events TO bpc_e1_identity_exec;
GRANT INSERT(invitation_id) ON bpc_security.events TO bpc_e1_tenant_exec;
-- Existing ALL policies must become SELECT-only before new INSERT grants: permissive
-- policies combine with OR, so leaving the old ALL policies would bypass new checks.
DROP POLICY self_consumption ON identity.identity_consumptions;
CREATE POLICY self_consumption ON identity.identity_consumptions FOR SELECT TO bpc_e1_identity_exec USING(identity_id=bpc_security.ctx('app.identity_id'));
DROP POLICY self_account ON identity.enterprise_user_accounts;
CREATE POLICY self_account ON identity.enterprise_user_accounts FOR SELECT TO bpc_e1_identity_exec USING(identity_id=bpc_security.ctx('app.identity_id'));
DROP POLICY self_membership ON tenancy.memberships;
CREATE POLICY self_membership ON tenancy.memberships FOR SELECT TO bpc_e1_identity_exec USING(account_id=bpc_security.ctx('app.account_id'));
CREATE POLICY accepted_consumption_insert ON identity.identity_consumptions FOR INSERT TO bpc_e1_identity_exec WITH CHECK(identity_id=bpc_security.ctx('app.identity_id') AND organization_id=bpc_security.ctx('app.organization_id') AND origin='Invitation');
CREATE POLICY accepted_account_insert ON identity.enterprise_user_accounts FOR INSERT TO bpc_e1_identity_exec WITH CHECK(identity_id=bpc_security.ctx('app.identity_id') AND organization_id=bpc_security.ctx('app.organization_id') AND status='Active');
CREATE POLICY accepted_member_insert ON tenancy.memberships FOR INSERT TO bpc_e1_identity_exec WITH CHECK(account_id=bpc_security.ctx('app.account_id') AND organization_id=bpc_security.ctx('app.organization_id') AND role='Member' AND status='Active' AND version=1);
CREATE POLICY accepted_event_insert ON bpc_security.events FOR INSERT TO bpc_e1_identity_exec WITH CHECK(actor_id=bpc_security.ctx('app.identity_id') AND organization_id=bpc_security.ctx('app.organization_id') AND kind='InvitationAccepted');
CREATE POLICY invitation_integrity ON tenancy.invitations FOR SELECT TO bpc_e1_integrity_exec USING(organization_id=bpc_security.ctx('app.integrity_org'));
CREATE POLICY consumption_integrity ON identity.identity_consumptions FOR SELECT TO bpc_e1_integrity_exec USING(organization_id=bpc_security.ctx('app.integrity_org'));
GRANT SELECT(identity_id,organization_id,account_id,origin) ON identity.identity_consumptions TO bpc_e1_integrity_exec;
CREATE FUNCTION bpc_security.invitation_integrity() RETURNS trigger LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE org uuid:=NEW.organization_id; i record;
BEGIN
 PERFORM set_config('app.integrity_org',org::text,true);
 IF EXISTS(SELECT FROM identity.identity_consumptions c WHERE c.organization_id=org AND c.origin='Invitation' AND NOT EXISTS(SELECT FROM tenancy.invitations x JOIN tenancy.memberships m ON m.membership_id=x.membership_id AND m.account_id=x.account_id AND m.organization_id=x.organization_id WHERE x.status='Accepted' AND x.organization_id=c.organization_id AND x.recipient_identity_id=c.identity_id AND x.account_id=c.account_id)) THEN
  RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Invitation binding required'; END IF;
 FOR i IN SELECT * FROM tenancy.invitations WHERE organization_id=org LOOP
  IF i.status='Accepted' AND NOT EXISTS(SELECT FROM identity.identity_consumptions c JOIN tenancy.memberships m ON m.organization_id=c.organization_id AND m.account_id=c.account_id WHERE c.identity_id=i.recipient_identity_id AND c.organization_id=i.organization_id AND c.account_id=i.account_id AND c.origin='Invitation' AND m.membership_id=i.membership_id) THEN
   RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Invitation binding required'; END IF;
  IF i.status='Replaced' AND NOT EXISTS(SELECT FROM tenancy.invitations s WHERE s.invitation_id=i.successor_id AND s.predecessor_id=i.invitation_id AND s.organization_id=i.organization_id AND s.recipient_identity_id=i.recipient_identity_id) THEN
   RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Invitation successor required'; END IF;
 END LOOP; RETURN NULL;
END $$;
ALTER FUNCTION bpc_security.invitation_integrity() OWNER TO bpc_e1_integrity_exec;
REVOKE ALL ON FUNCTION bpc_security.invitation_integrity() FROM PUBLIC;
CREATE CONSTRAINT TRIGGER invitation_integrity AFTER INSERT OR UPDATE ON tenancy.invitations DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION bpc_security.invitation_integrity();
CREATE CONSTRAINT TRIGGER invitation_consumption_integrity AFTER INSERT ON identity.identity_consumptions DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION bpc_security.invitation_integrity();
CREATE FUNCTION bpc_security.invitation_json(i tenancy.invitations) RETURNS jsonb LANGUAGE sql VOLATILE SET search_path=pg_catalog,pg_temp AS $$
 SELECT jsonb_build_object('invitationId',i.invitation_id,'organizationId',i.organization_id,'recipientUserId',i.recipient_identity_id,'role','Member','organizationName',(SELECT name FROM tenancy.organizations WHERE organization_id=i.organization_id),'state',CASE WHEN i.status='Pending' AND i.expires_at<=clock_timestamp() THEN 'Expired' ELSE i.status END,'version',i.version,'createdAtUtc',i.created_at,'expiresAtUtc',i.expires_at,'accountId',i.account_id,'membershipId',i.membership_id)
$$;
ALTER FUNCTION bpc_security.invitation_json(tenancy.invitations) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.invitation_json(tenancy.invitations) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION bpc_security.invitation_json(tenancy.invitations) TO bpc_e1_identity_exec;

CREATE FUNCTION bpc_security.manage_invitation(p_issuer text,p_subject text,p_org uuid,p_action text,p_id uuid,p_recipient uuid,p_reference bytea,p_secret bytea,p_version bigint,p_key uuid,p_limit integer,p_before timestamptz,p_before_id uuid,p_state text DEFAULT NULL)
RETURNS jsonb LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE ctx jsonb; actor uuid; recipient uuid; target tenancy.invitations; predecessor tenancy.invitations; receipt record;
 fingerprint bytea; now_at timestamptz; eligible boolean; result jsonb; new_id uuid:=gen_random_uuid(); old_id uuid;
BEGIN
 ctx:=bpc_security.resolve_context(p_issuer,p_subject);
 IF ctx->>'state' IS DISTINCT FROM 'Linked' OR (ctx->>'organizationId')::uuid IS DISTINCT FROM p_org THEN RAISE EXCEPTION USING ERRCODE='P1804',MESSAGE='Invitation unavailable'; END IF;
 IF ctx->>'role' IS NULL OR ctx->>'role' NOT IN ('Owner','Admin') THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Invitation unavailable'; END IF;
 actor:=(ctx->>'identityId')::uuid;
 IF p_action IS NULL OR p_action NOT IN ('Get','List','Issue','Cancel','Replace') THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Invitation unavailable'; END IF;
 IF p_action='List' THEN
  IF (p_state IS NOT NULL AND p_state NOT IN ('Pending','Accepted','Expired','Cancelled','Replaced')) OR p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 100 OR (p_before IS NULL)<>(p_before_id IS NULL) OR NOT coalesce(isfinite(p_before),true) THEN RAISE EXCEPTION USING ERRCODE='P1800',MESSAGE='Invalid page'; END IF;
  SELECT coalesce(jsonb_agg(bpc_security.invitation_json(x) ORDER BY x.created_at DESC,x.invitation_id DESC),'[]'::jsonb) INTO result
   FROM (SELECT i.* FROM tenancy.invitations i WHERE i.organization_id=p_org AND (p_state IS NULL OR (CASE WHEN i.status='Pending' AND i.expires_at<=clock_timestamp() THEN 'Expired' ELSE i.status END)=p_state) AND (p_before IS NULL OR (i.created_at,i.invitation_id)<(p_before,p_before_id)) ORDER BY i.created_at DESC,i.invitation_id DESC LIMIT p_limit+1) x;
  RETURN result;
 END IF;
 IF p_action<>'Issue' THEN
  SELECT * INTO target FROM tenancy.invitations WHERE organization_id=p_org AND invitation_id=p_id;
  IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P1804',MESSAGE='Invitation unavailable'; END IF;
  IF p_action='Get' THEN RETURN jsonb_build_object('invitation',bpc_security.invitation_json(target),'replayed',false); END IF;
  recipient:=target.recipient_identity_id;
 ELSE recipient:=p_recipient; END IF;
 IF p_action IN ('Issue','Replace') THEN
  IF p_key IS NULL OR p_key='00000000-0000-0000-0000-000000000000'::uuid OR p_secret IS NULL OR octet_length(p_secret)<>32 THEN RAISE EXCEPTION USING ERRCODE='P1800',MESSAGE='Invalid operation'; END IF;
  fingerprint:=bpc_crypto.digest(convert_to(jsonb_build_array(p_action,p_id,p_recipient,encode(p_reference,'hex'),p_version)::text,'UTF8'),'sha256');
  PERFORM pg_advisory_xact_lock(hashtextextended(actor::text||p_org::text||p_action||p_key::text,8));
 END IF;
 eligible:=bpc_security.invitation_identity_locks(actor,recipient);
 PERFORM set_config('app.identity_id',actor::text,true);
 PERFORM 1 FROM tenancy.organizations WHERE organization_id=p_org FOR UPDATE;
 PERFORM 1 FROM identity.enterprise_user_accounts WHERE organization_id=p_org ORDER BY account_id FOR UPDATE;
 PERFORM 1 FROM tenancy.memberships WHERE organization_id=p_org ORDER BY membership_id FOR UPDATE;
 ctx:=bpc_security.resolve_context(p_issuer,p_subject);
 IF ctx->>'state' IS DISTINCT FROM 'Linked' OR (ctx->>'organizationId')::uuid IS DISTINCT FROM p_org THEN RAISE EXCEPTION USING ERRCODE='P1804',MESSAGE='Invitation unavailable'; END IF;
 IF ctx->>'role' IS NULL OR ctx->>'role' NOT IN ('Owner','Admin') THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Invitation unavailable'; END IF;
 now_at:=clock_timestamp();
 IF p_action IN ('Issue','Replace') THEN
  SELECT * INTO receipt FROM bpc_security.invitation_receipts WHERE actor_identity_id=actor AND organization_id=p_org AND action=p_action AND operation_id=p_key;
  IF FOUND THEN
   IF receipt.request_hash<>fingerprint THEN RAISE EXCEPTION USING ERRCODE='P1003',MESSAGE='Operation conflict'; END IF;
   IF receipt.expires_at<=now_at THEN RAISE EXCEPTION USING ERRCODE='P1809',MESSAGE='Operation conflict'; END IF;
   SELECT * INTO target FROM tenancy.invitations WHERE invitation_id=receipt.invitation_id AND organization_id=p_org;
   RETURN jsonb_build_object('invitation',bpc_security.invitation_json(target),'replayed',true);
  END IF;
 END IF;
 INSERT INTO tenancy.invitation_slots(organization_id,recipient_identity_id) VALUES(p_org,recipient) ON CONFLICT DO NOTHING;
 PERFORM 1 FROM tenancy.invitation_slots WHERE organization_id=p_org AND recipient_identity_id=recipient FOR UPDATE;
 IF p_action='Issue' THEN
  -- C02 proof is checked inside SQL, not a client-supplied eligibility boolean.
  IF NOT bpc_security.invitation_reference_current(recipient,p_reference) THEN RAISE EXCEPTION USING ERRCODE='P1004',MESSAGE='Recipient unavailable'; END IF; PERFORM set_config('app.identity_id',actor::text,true);
  IF NOT eligible THEN RAISE EXCEPTION USING ERRCODE='P1804',MESSAGE='Recipient unavailable'; END IF;
  SELECT * INTO predecessor FROM tenancy.invitations WHERE organization_id=p_org AND recipient_identity_id=recipient AND status='Pending' FOR UPDATE;
  IF FOUND THEN
   IF predecessor.expires_at>now_at THEN RAISE EXCEPTION USING ERRCODE='P1809',MESSAGE='Invitation conflict'; END IF;
   UPDATE tenancy.invitations SET status='Expired',version=version+1 WHERE invitation_id=predecessor.invitation_id;
   INSERT INTO bpc_security.events(event_id,operation_id,organization_id,actor_id,kind,invitation_id) VALUES(gen_random_uuid(),p_key,p_org,actor,'InvitationExpired',predecessor.invitation_id);
  END IF;
  INSERT INTO tenancy.invitations(invitation_id,organization_id,recipient_identity_id,issuer_membership_id,reference_hash,secret_hash,status,created_at,expires_at)
   VALUES(new_id,p_org,recipient,(ctx->>'membershipId')::uuid,p_reference,p_secret,'Pending',now_at,now_at+interval '24 hours') RETURNING * INTO target;
 ELSE
  SELECT * INTO target FROM tenancy.invitations WHERE invitation_id=p_id AND organization_id=p_org FOR UPDATE;
  IF target.version IS DISTINCT FROM p_version THEN RAISE EXCEPTION USING ERRCODE='P1002',MESSAGE='Version conflict'; END IF;
  IF p_action='Cancel' THEN
   IF target.status<>'Pending' OR target.expires_at<=now_at THEN RAISE EXCEPTION USING ERRCODE='P1809',MESSAGE='Invitation conflict'; END IF;
   UPDATE tenancy.invitations SET status='Cancelled',version=version+1 WHERE invitation_id=p_id RETURNING * INTO target;
  ELSE
   IF NOT eligible OR target.status NOT IN ('Pending','Expired') THEN RAISE EXCEPTION USING ERRCODE='P1809',MESSAGE='Invitation conflict'; END IF;
   IF target.status='Pending' AND target.expires_at<=now_at THEN
    UPDATE tenancy.invitations SET status='Expired',version=version+1 WHERE invitation_id=p_id;
    INSERT INTO bpc_security.events(event_id,operation_id,organization_id,actor_id,kind,invitation_id) VALUES(gen_random_uuid(),p_key,p_org,actor,'InvitationExpired',p_id);
   END IF;
   UPDATE tenancy.invitations SET status='Replaced',successor_id=new_id,version=version+1 WHERE invitation_id=p_id RETURNING * INTO predecessor;
   INSERT INTO tenancy.invitations(invitation_id,organization_id,recipient_identity_id,issuer_membership_id,reference_hash,secret_hash,status,created_at,expires_at,predecessor_id)
    VALUES(new_id,p_org,recipient,(ctx->>'membershipId')::uuid,predecessor.reference_hash,p_secret,'Pending',now_at,now_at+interval '24 hours',p_id) RETURNING * INTO target;
  END IF;
 END IF;
 INSERT INTO bpc_security.events(event_id,operation_id,organization_id,actor_id,kind,invitation_id)
  VALUES(gen_random_uuid(),coalesce(p_key,gen_random_uuid()),p_org,actor,CASE p_action WHEN 'Issue' THEN 'InvitationIssued' WHEN 'Cancel' THEN 'InvitationCancelled' ELSE 'InvitationReplaced' END,CASE WHEN p_action='Replace' THEN p_id ELSE target.invitation_id END);
 IF p_action IN ('Issue','Replace') THEN
  INSERT INTO bpc_security.invitation_receipts VALUES(actor,p_org,p_action,p_key,fingerprint,target.invitation_id,now_at,now_at+interval '48 hours');
 END IF;
 RETURN jsonb_build_object('invitation',bpc_security.invitation_json(target),'replayed',false);
END $$;
ALTER FUNCTION bpc_security.manage_invitation(text,text,uuid,text,uuid,uuid,bytea,bytea,bigint,uuid,integer,timestamptz,uuid,text) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.manage_invitation(text,text,uuid,text,uuid,uuid,bytea,bytea,bigint,uuid,integer,timestamptz,uuid,text) FROM PUBLIC;

CREATE FUNCTION bpc_security.recipient_invitation(p_issuer text,p_subject text,p_id uuid,p_hash bytea,p_accept boolean,p_version bigint) RETURNS jsonb
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE ctx jsonb; id uuid; target tenancy.invitations; acc uuid; mem uuid; now_at timestamptz; unused boolean;
BEGIN
 ctx:=bpc_security.resolve_context(p_issuer,p_subject);id:=(ctx->>'identityId')::uuid;
 IF id IS NULL OR p_hash IS NULL OR octet_length(p_hash)<>32 THEN RAISE EXCEPTION USING ERRCODE='P1804',MESSAGE='Invitation unavailable'; END IF;
 SELECT * INTO target FROM tenancy.invitations WHERE invitation_id=p_id AND recipient_identity_id=id AND secret_hash=p_hash;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P1804',MESSAGE='Invitation unavailable'; END IF;
 IF ctx->>'state' NOT IN ('NotLinked','Linked') THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Access unavailable'; END IF;
 IF ctx->>'state'='Linked' AND (target.status<>'Accepted' OR target.account_id IS DISTINCT FROM (ctx->>'accountId')::uuid) THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Account ineligible'; END IF;
 IF p_accept THEN
  unused:=bpc_security.invitation_identity_locks(id,id);
  PERFORM set_config('app.identity_id',id::text,true); PERFORM set_config('app.organization_id',target.organization_id::text,true);
  PERFORM 1 FROM tenancy.organizations WHERE organization_id=target.organization_id FOR UPDATE;
  -- Revalidate the authoritative binding after the identity/organization waits.
  ctx:=bpc_security.resolve_context(p_issuer,p_subject);
  IF ctx->>'state' NOT IN ('NotLinked','Linked') THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Access unavailable'; END IF;
  PERFORM set_config('app.organization_id',target.organization_id::text,true);
  -- No existing Account/Membership is mutated by acceptance; rows are inserted last.
  PERFORM 1 FROM tenancy.invitation_slots WHERE organization_id=target.organization_id AND recipient_identity_id=id FOR UPDATE;
  SELECT * INTO target FROM tenancy.invitations WHERE invitation_id=p_id AND recipient_identity_id=id AND secret_hash=p_hash FOR UPDATE;
 END IF;
 PERFORM set_config('app.organization_id',target.organization_id::text,true);
 IF NOT EXISTS(SELECT FROM tenancy.organizations WHERE organization_id=target.organization_id AND status='Active') THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Access unavailable'; END IF;
 IF ctx->>'state'='Linked' AND (target.status<>'Accepted' OR target.account_id IS DISTINCT FROM (ctx->>'accountId')::uuid) THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Account ineligible'; END IF;
 IF target.status='Accepted' THEN
  IF ctx->>'state'<>'Linked' OR target.account_id IS DISTINCT FROM (ctx->>'accountId')::uuid THEN RAISE EXCEPTION USING ERRCODE='P1803',MESSAGE='Access unavailable'; END IF;
  RETURN jsonb_build_object('invitation',bpc_security.invitation_json(target),'replayed',true);
 END IF;
 IF NOT p_accept THEN RETURN jsonb_build_object('invitation',bpc_security.invitation_json(target),'replayed',false); END IF;
 now_at:=clock_timestamp();
 IF target.status<>'Pending' OR target.expires_at<=now_at THEN RAISE EXCEPTION USING ERRCODE='P1809',MESSAGE='Invitation conflict'; END IF;
 IF target.version IS DISTINCT FROM p_version THEN RAISE EXCEPTION USING ERRCODE='P1002',MESSAGE='Version conflict'; END IF;
 acc:=gen_random_uuid();mem:=gen_random_uuid();PERFORM set_config('app.account_id',acc::text,true);
 INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id,origin) VALUES(id,target.organization_id,acc,'Invitation');
 INSERT INTO identity.enterprise_user_accounts(account_id,identity_id,organization_id,status) VALUES(acc,id,target.organization_id,'Active');
 INSERT INTO tenancy.memberships(membership_id,organization_id,account_id,status,role) VALUES(mem,target.organization_id,acc,'Active','Member');
 UPDATE tenancy.invitations SET status='Accepted',version=version+1,accepted_at=now_at,account_id=acc,membership_id=mem WHERE invitation_id=p_id RETURNING * INTO target;
 INSERT INTO bpc_security.events(event_id,operation_id,organization_id,actor_id,target_account_id,kind,invitation_id) VALUES(gen_random_uuid(),gen_random_uuid(),target.organization_id,id,acc,'InvitationAccepted',p_id);
 RETURN jsonb_build_object('invitation',bpc_security.invitation_json(target),'replayed',false);
END $$;
ALTER FUNCTION bpc_security.recipient_invitation(text,text,uuid,bytea,boolean,bigint) OWNER TO bpc_e1_identity_exec;
REVOKE ALL ON FUNCTION bpc_security.recipient_invitation(text,text,uuid,bytea,boolean,bigint) FROM PUBLIC;

CREATE OR REPLACE FUNCTION bpc_security.persistence_ready() RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT (SELECT array_agg(version ORDER BY version)=ARRAY[1,2,3,4,5,6,7,8] AND bool_and(length(checksum)=64) FROM bpc_migrations.versions)
 AND (SELECT count(*)=16 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='bpc_e1_ddl') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security') AND c.relkind='r')
 AND (SELECT count(*)=6 AND bool_and(NOT rolsuper AND NOT rolbypassrls AND NOT rolcanlogin AND NOT rolcreaterole AND NOT rolcreatedb) FROM pg_roles WHERE rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.roleid WHERE r.rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace CROSS JOIN LATERAL aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) a WHERE n.nspname IN ('bpc_security','bpc_crypto') AND a.grantee=0 AND a.privilege_type='EXECUTE')
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='bpc_security' AND (p.prosecdef AND pg_get_userbyid(p.proowner)='bpc_e1_ddl' OR NOT coalesce(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp'],false)))
 AND to_regprocedure('bpc_security.read_tenant_audit(text,text,integer,timestamptz,uuid)') IS NULL
$$;
