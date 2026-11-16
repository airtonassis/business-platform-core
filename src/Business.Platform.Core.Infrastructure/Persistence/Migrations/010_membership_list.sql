-- E2-05 read-only increment: no grants on tables, no new role, no membership mutations.
CREATE FUNCTION bpc_security.list_tenant_memberships(p_issuer text,p_subject text,p_org uuid,p_limit integer,p_offset integer)
RETURNS jsonb LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE ctx jsonb; result jsonb;
BEGIN
 ctx:=bpc_security.resolve_context(p_issuer,p_subject);
 IF ctx->>'state' IS DISTINCT FROM 'Linked' OR (ctx->>'organizationId')::uuid IS DISTINCT FROM p_org THEN RAISE EXCEPTION USING ERRCODE='P1704',MESSAGE='Resource unavailable'; END IF;
 IF ctx->>'role' IS NULL OR ctx->>'role' NOT IN ('Owner','Admin') THEN RAISE EXCEPTION USING ERRCODE='P1703',MESSAGE='Access unavailable'; END IF;
 IF p_limit IS NULL OR p_limit NOT BETWEEN 1 AND 100 OR p_offset IS NULL OR p_offset NOT BETWEEN 0 AND 10000 THEN RAISE EXCEPTION USING ERRCODE='P1700',MESSAGE='Invalid page'; END IF;
 SELECT jsonb_build_object('items',coalesce(jsonb_agg(value ORDER BY membership_id) FILTER(WHERE rn<=p_limit),'[]'::jsonb),'hasMore',count(*)>p_limit) INTO result FROM (
 SELECT membership_id,row_number() OVER(ORDER BY membership_id) rn,jsonb_build_object('membershipId',membership_id,'accountId',account_id,'organizationId',organization_id,'role',role,'status',status,'accountActive',account_active,'version',version) value
 FROM (SELECT m.membership_id,m.account_id,m.organization_id,m.role,m.status,m.version,a.status='Active' account_active
 FROM tenancy.memberships m JOIN identity.enterprise_user_accounts a USING(organization_id,account_id)
 WHERE m.organization_id=p_org ORDER BY m.membership_id LIMIT p_limit+1 OFFSET p_offset) q) rows;
 RETURN result;
END $$;
ALTER FUNCTION bpc_security.list_tenant_memberships(text,text,uuid,integer,integer) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.list_tenant_memberships(text,text,uuid,integer,integer) FROM PUBLIC;
-- Only an authorized operator grants EXECUTE on this port to Tenant runtime.

CREATE OR REPLACE FUNCTION bpc_security.persistence_ready() RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT (SELECT array_agg(version ORDER BY version)=ARRAY[1,2,3,4,5,6,7,8,9,10] AND bool_and(length(checksum)=64) FROM bpc_migrations.versions)
 AND (SELECT count(*)=19 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='bpc_e1_ddl') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security','sandbox') AND c.relkind='r')
 AND (SELECT count(*)=6 AND bool_and(NOT rolsuper AND NOT rolbypassrls AND NOT rolcanlogin AND NOT rolcreaterole AND NOT rolcreatedb) FROM pg_roles WHERE rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.roleid WHERE r.rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace CROSS JOIN LATERAL aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) a WHERE n.nspname IN ('bpc_security','bpc_crypto','sandbox') AND a.grantee=0 AND a.privilege_type='EXECUTE')
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='bpc_security' AND (p.prosecdef AND pg_get_userbyid(p.proowner)='bpc_e1_ddl' OR NOT coalesce(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp'],false)))
 AND to_regprocedure('bpc_security.read_tenant_audit(text,text,integer,timestamptz,uuid)') IS NULL
$$;
