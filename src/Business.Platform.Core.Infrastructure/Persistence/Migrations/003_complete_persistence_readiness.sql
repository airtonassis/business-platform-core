-- Product E1 readiness checks the complete migration set and effective security shape.
CREATE OR REPLACE FUNCTION bpc_security.persistence_ready() RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT (SELECT array_agg(version ORDER BY version)=ARRAY[1,2,3] AND bool_and(length(checksum)=64) FROM bpc_migrations.versions)
 AND (SELECT count(*)=8 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='bpc_e1_ddl') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security') AND c.relkind='r')
 AND (SELECT count(*)=6 AND bool_and(NOT rolsuper AND NOT rolbypassrls AND NOT rolcanlogin AND NOT rolcreaterole AND NOT rolcreatedb) FROM pg_roles WHERE rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.roleid WHERE r.rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace CROSS JOIN LATERAL aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) a WHERE n.nspname='bpc_security' AND a.grantee=0 AND a.privilege_type='EXECUTE')
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='bpc_security' AND (p.prosecdef AND pg_get_userbyid(p.proowner)='bpc_e1_ddl' OR NOT coalesce(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp'],false)))
$$;
