-- E2-02: explicit C08 commands, same five executors, no runtime logins or secrets.
GRANT UPDATE(role) ON tenancy.memberships TO bpc_e1_tenant_exec;
ALTER TABLE bpc_security.events DROP CONSTRAINT events_kind_check;
ALTER TABLE bpc_security.events ADD CONSTRAINT events_kind_check CHECK(kind IN ('OrganizationBootstrapped','MembershipRevoked','MembershipRoleChanged'));

CREATE FUNCTION bpc_security.tenant_membership(p_issuer text,p_subject text,p_org uuid,p_target uuid,p_action text,p_version bigint,p_operation uuid)
RETURNS jsonb LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE ctx jsonb; actor uuid; target record; st text;
BEGIN
 ctx:=bpc_security.resolve_context(p_issuer,p_subject);
 IF ctx->>'state' IS DISTINCT FROM 'Linked' OR (ctx->>'organizationId')::uuid IS DISTINCT FROM p_org THEN
  RAISE EXCEPTION USING ERRCODE='P1704',MESSAGE='Membership unavailable'; END IF;
 IF ctx->>'role' IS NULL OR ctx->>'role' NOT IN ('Owner','Admin') THEN
  RAISE EXCEPTION USING ERRCODE='P1703',MESSAGE='Membership unavailable'; END IF;
 actor:=(ctx->>'identityId')::uuid;
 IF p_action IS NULL OR p_action NOT IN ('Read','Promote','Demote','Revoke') THEN
  RAISE EXCEPTION USING ERRCODE='P1703',MESSAGE='Membership unavailable'; END IF;
 IF p_action<>'Read' THEN
  -- Same lock order as revoke_membership: identity, organization, accounts, memberships.
  PERFORM 1 FROM identity.identities WHERE identity_id=actor FOR SHARE;
  PERFORM 1 FROM tenancy.organizations WHERE organization_id=p_org FOR UPDATE;
  PERFORM 1 FROM identity.enterprise_user_accounts WHERE organization_id=p_org ORDER BY account_id FOR UPDATE;
  PERFORM 1 FROM tenancy.memberships WHERE organization_id=p_org ORDER BY membership_id FOR UPDATE;
  -- New READ COMMITTED statement after lock waits: never authorize from the preliminary snapshot.
  ctx:=bpc_security.resolve_context(p_issuer,p_subject);
  IF ctx->>'state' IS DISTINCT FROM 'Linked' OR (ctx->>'organizationId')::uuid IS DISTINCT FROM p_org THEN
   RAISE EXCEPTION USING ERRCODE='P1704',MESSAGE='Membership unavailable'; END IF;
 END IF;
 SELECT m.membership_id,m.account_id,m.organization_id,m.role,m.status,m.version,a.status='Active' AS account_active
 INTO target FROM tenancy.memberships m JOIN identity.enterprise_user_accounts a USING(organization_id,account_id)
 WHERE m.organization_id=p_org AND m.membership_id=p_target;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P1704',MESSAGE='Membership unavailable'; END IF;
 IF ctx->>'role' IS NULL OR ctx->>'role' NOT IN ('Owner','Admin') THEN
  RAISE EXCEPTION USING ERRCODE='P1703',MESSAGE='Membership unavailable'; END IF;
 IF p_action<>'Read' THEN
  IF target.account_id=(ctx->>'accountId')::uuid OR target.role='Owner' OR target.status<>'Active' OR NOT target.account_active
   OR (p_action='Promote' AND (ctx->>'role'<>'Owner' OR target.role<>'Member'))
   OR (p_action='Demote' AND (ctx->>'role'<>'Owner' OR target.role<>'Admin'))
   OR (p_action='Revoke' AND ctx->>'role'='Admin' AND target.role<>'Member') THEN
   RAISE EXCEPTION USING ERRCODE='P1703',MESSAGE='Membership unavailable'; END IF;
  IF p_version IS NULL OR p_version<>target.version THEN RAISE EXCEPTION USING ERRCODE='P1002',MESSAGE='Version conflict'; END IF;
  IF p_operation IS NULL OR p_operation='00000000-0000-0000-0000-000000000000'::uuid THEN
   RAISE EXCEPTION USING ERRCODE='P1703',MESSAGE='Membership unavailable'; END IF;
  IF p_action='Revoke' THEN
   -- Preserve existing joint Account/Membership revocation and its atomic event.
   PERFORM bpc_security.revoke_membership(actor,p_org,p_target,p_version,p_operation);
   target.status:='Revoked'; target.account_active:=false;
  ELSE
   UPDATE tenancy.memberships SET role=CASE p_action WHEN 'Promote' THEN 'Admin' ELSE 'Member' END,version=version+1
    WHERE membership_id=p_target AND organization_id=p_org;
   target.role:=CASE p_action WHEN 'Promote' THEN 'Admin' ELSE 'Member' END;
   INSERT INTO bpc_security.events(event_id,operation_id,organization_id,actor_id,target_account_id,kind)
    VALUES(gen_random_uuid(),p_operation,p_org,actor,target.account_id,'MembershipRoleChanged');
  END IF;
  target.version:=target.version+1;
 END IF;
 RETURN jsonb_build_object('membershipId',target.membership_id,'accountId',target.account_id,'organizationId',target.organization_id,
  'role',target.role,'status',target.status,'accountActive',target.account_active,'version',target.version);
END $$;
ALTER FUNCTION bpc_security.tenant_membership(text,text,uuid,uuid,text,bigint,uuid) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.tenant_membership(text,text,uuid,uuid,text,bigint,uuid) FROM PUBLIC;
-- An authorized operator may grant only EXECUTE on this entry to a separate Tenant login.
-- Never grant UPDATE(role/status), old revoke_membership or owner-role membership to a runtime login.

CREATE OR REPLACE FUNCTION bpc_security.persistence_ready() RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT (SELECT array_agg(version ORDER BY version)=ARRAY[1,2,3,4,5,6,7] AND bool_and(length(checksum)=64) FROM bpc_migrations.versions)
 AND (SELECT count(*)=13 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='bpc_e1_ddl') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security') AND c.relkind='r')
 AND (SELECT count(*)=6 AND bool_and(NOT rolsuper AND NOT rolbypassrls AND NOT rolcanlogin AND NOT rolcreaterole AND NOT rolcreatedb) FROM pg_roles WHERE rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.roleid WHERE r.rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace CROSS JOIN LATERAL aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) a WHERE n.nspname IN ('bpc_security','bpc_crypto') AND a.grantee=0 AND a.privilege_type='EXECUTE')
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='bpc_security' AND (p.prosecdef AND pg_get_userbyid(p.proowner)='bpc_e1_ddl' OR NOT coalesce(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp'],false)))
 AND to_regprocedure('bpc_security.read_tenant_audit(text,text,integer,timestamptz,uuid)') IS NULL
$$;
