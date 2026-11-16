-- Additive Tenant audit read port. Apply only through an explicitly authorized migrator.
-- No runtime login/secret here. Preserve INSERT used by bootstrap and revoke_membership.
GRANT SELECT(event_id,occurred_at,kind,target_account_id,organization_id) ON bpc_security.events TO bpc_e1_tenant_exec;
GRANT EXECUTE ON FUNCTION bpc_security.resolve_context(text,text) TO bpc_e1_tenant_exec;

CREATE FUNCTION bpc_security.read_tenant_audit(p_issuer text,p_subject text,p_limit integer,
 p_before_time timestamptz DEFAULT NULL,p_before_event uuid DEFAULT NULL)
RETURNS TABLE(event_id uuid,occurred_at timestamptz,kind text,target_account_id uuid)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE context jsonb; org uuid;
BEGIN
 IF p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 100 OR
    (p_before_time IS NULL)<>(p_before_event IS NULL) OR
    p_before_time IS NOT NULL AND (NOT isfinite(p_before_time) OR p_before_event='00000000-0000-0000-0000-000000000000'::uuid)
 THEN RAISE EXCEPTION USING ERRCODE='P1400',MESSAGE='Invalid page'; END IF;
 context:=bpc_security.resolve_context(p_issuer,p_subject);
 IF context->>'state'='Inconsistent' THEN RAISE EXCEPTION USING ERRCODE='P1500',MESSAGE='Context inconsistent'; END IF;
 IF context->>'state' IS DISTINCT FROM 'Linked' THEN RAISE EXCEPTION USING ERRCODE='P1404',MESSAGE='Access unavailable'; END IF;
 IF context->>'role' IS NULL OR context->>'role' NOT IN ('Owner','Admin') THEN
  RAISE EXCEPTION USING ERRCODE='P1403',MESSAGE='Access unavailable'; END IF;
 org:=(context->>'organizationId')::uuid;
 IF org IS NULL THEN RAISE EXCEPTION USING ERRCODE='P1500',MESSAGE='Context inconsistent'; END IF;
 -- All context is server-resolved; ignore any preexisting GUCs on a pooled connection.
 PERFORM set_config('app.organization_id',org::text,true);
 PERFORM set_config('app.account_id',context->>'accountId',true);
 RETURN QUERY SELECT e.event_id,e.occurred_at,e.kind,e.target_account_id
 FROM bpc_security.events e
 WHERE e.organization_id=org AND e.kind IN ('OrganizationBootstrapped','MembershipRevoked')
 AND (p_before_time IS NULL OR (e.occurred_at,e.event_id)<(p_before_time,p_before_event))
 ORDER BY e.occurred_at DESC,e.event_id DESC LIMIT p_limit+1;
END $$;
ALTER FUNCTION bpc_security.read_tenant_audit(text,text,integer,timestamptz,uuid) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.read_tenant_audit(text,text,integer,timestamptz,uuid) FROM PUBLIC;

CREATE FUNCTION bpc_security.audit_immutable() RETURNS trigger LANGUAGE plpgsql
SET search_path=pg_catalog,pg_temp AS $$
BEGIN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Immutable audit event'; END $$;
ALTER FUNCTION bpc_security.audit_immutable() OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.audit_immutable() FROM PUBLIC;
CREATE TRIGGER audit_event_immutable BEFORE UPDATE OR DELETE ON bpc_security.events
FOR EACH ROW EXECUTE FUNCTION bpc_security.audit_immutable();

-- No index change without a demonstrated plan/performance need. Existing org/time index remains.
CREATE OR REPLACE FUNCTION bpc_security.persistence_ready() RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT (SELECT array_agg(version ORDER BY version)=ARRAY[1,2,3,4,5] AND bool_and(length(checksum)=64) FROM bpc_migrations.versions)
 AND (SELECT count(*)=12 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='bpc_e1_ddl') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security') AND c.relkind='r')
 AND (SELECT count(*)=6 AND bool_and(NOT rolsuper AND NOT rolbypassrls AND NOT rolcanlogin AND NOT rolcreaterole AND NOT rolcreatedb) FROM pg_roles WHERE rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.roleid WHERE r.rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace CROSS JOIN LATERAL aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) a WHERE n.nspname='bpc_security' AND a.grantee=0 AND a.privilege_type='EXECUTE')
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='bpc_security' AND (p.prosecdef AND pg_get_userbyid(p.proowner)='bpc_e1_ddl' OR NOT coalesce(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp'],false)))
$$;
