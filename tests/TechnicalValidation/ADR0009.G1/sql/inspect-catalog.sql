-- ADR-0009 G1 inspection only. F01-F07/F10/F11/V06/V09/V12.
-- No schema, role, function or fixture is created. NOT EXECUTED.
-- Run only through Collect-Catalog.ps1 after a separate authorization.
\set ON_ERROR_STOP on
\if :{?expected_database}
\else
\echo Missing approved target variables.
\quit 3
\endif
\if :{?isolation_marker}
\else
\quit 3
\endif
\if :{?observer_user}
\else
\quit 3
\endif
\if :{?server_version_num}
\else
\quit 3
\endif

BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL search_path = pg_catalog, pg_temp;
SELECT
  current_database() = :'expected_database'
  AND current_database() ~ '^bpc_adr0009_g1_[a-z0-9_]+$'
  AND session_user = :'observer_user'
  AND NOT r.rolsuper AND NOT r.rolbypassrls
  AND current_setting('server_version_num') = :'server_version_num'
  AND inet_server_addr() IN ('127.0.0.1'::inet, '::1'::inet)
  AND obj_description(d.oid, 'pg_database') = :'isolation_marker'
  AND current_setting('transaction_read_only') = 'on' AS guard_ok
FROM pg_catalog.pg_database d
JOIN pg_catalog.pg_roles r ON r.rolname = session_user
WHERE d.datname = current_database()
\gset
\if :guard_ok
\else
\echo Target rejected: isolation/identity/version marker mismatch.
\quit 3
\endif

SELECT json_build_object('kind','environment','database',current_database(),
 'sessionUser',session_user,'currentUser',current_user,'pid',pg_backend_pid(),
 'serverVersionNum',current_setting('server_version_num'),
 'readOnly',current_setting('transaction_read_only'),
 'status','COLLECTED_NOT_VALIDATED')::text;

SELECT json_build_object('kind','role','name',r.rolname,
 'canLogin',r.rolcanlogin,'superuser',r.rolsuper,'bypassRls',r.rolbypassrls,
 'inherit',r.rolinherit,'createRole',r.rolcreaterole,'createDb',r.rolcreatedb,
 'replication',r.rolreplication)::text
FROM pg_catalog.pg_roles r
WHERE r.rolname IN ('core_identity','core_tenant','core_platform','exec_identity',
 'exec_tenant','exec_bootstrap','exec_context','exec_integrity','core_migrator','ddl_owner','g1_probe');

-- Emit all membership edges reachable from a relevant starting role.
-- Optional version-specific membership options are preserved via to_jsonb.
WITH RECURSIVE reach(roleid) AS (
 SELECT oid FROM pg_catalog.pg_roles WHERE rolname IN
 ('core_identity','core_tenant','core_platform','exec_identity','exec_tenant',
  'exec_bootstrap','exec_context','exec_integrity')
 UNION SELECT m.roleid FROM pg_catalog.pg_auth_members m JOIN reach x ON m.member=x.roleid
)
SELECT json_build_object('kind','membership','member',member.rolname,
 'grantedRole',target.rolname,'admin',m.admin_option,
 'inheritOption',to_jsonb(m)->'inherit_option','setOption',to_jsonb(m)->'set_option')::text
FROM pg_catalog.pg_auth_members m
JOIN reach x ON x.roleid=m.member
JOIN pg_catalog.pg_roles member ON member.oid=m.member
JOIN pg_catalog.pg_roles target ON target.oid=m.roleid;

SELECT json_build_object('kind','schema','schema',n.nspname,'owner',pg_get_userbyid(n.nspowner),
 'acl',coalesce((SELECT json_agg(json_build_object('grantee',
 CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
 'privilege',a.privilege_type,'grantable',a.is_grantable))
 FROM aclexplode(coalesce(n.nspacl,acldefault('n',n.nspowner))) a),'[]'::json))::text
FROM pg_catalog.pg_namespace n WHERE (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema');

SELECT json_build_object('kind','table','name',n.nspname||'.'||c.relname,
 'owner',pg_get_userbyid(c.relowner),'relationKind',c.relkind,
 'rls',c.relrowsecurity,'forceRls',c.relforcerowsecurity,
 'acl',coalesce((SELECT json_agg(json_build_object('grantee',
 CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
 'privilege',a.privilege_type,'grantable',a.is_grantable))
 FROM aclexplode(coalesce(c.relacl,acldefault('r',c.relowner))) a),'[]'::json))::text
FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
WHERE (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema')
 AND c.relkind IN ('r','p','v','m','f');

SELECT json_build_object('kind','effectiveTablePrivilege','role',r.rolname,
 'table',n.nspname||'.'||c.relname,'privilege',p.name,
 'allowed',has_table_privilege(r.oid,c.oid,p.name))::text
FROM pg_catalog.pg_roles r
CROSS JOIN pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
CROSS JOIN (VALUES ('SELECT'),('INSERT'),('UPDATE'),('DELETE'),('TRUNCATE'),('REFERENCES'),('TRIGGER'),('MAINTAIN')) p(name)
WHERE r.rolname IN ('core_identity','core_tenant','core_platform','exec_identity',
 'exec_tenant','exec_bootstrap','exec_context','exec_integrity','g1_probe','ddl_owner','core_migrator')
 AND (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema')
 AND c.relkind IN ('r','p','v','m','f');

SELECT json_build_object('kind','effectiveColumnPrivilege','role',r.rolname,
 'table',n.nspname||'.'||c.relname,'column',a.attname,'privilege',p.name,
 'allowed',has_column_privilege(r.oid,c.oid,a.attnum,p.name),
 'declaredAcl',a.attacl::text)::text
FROM pg_catalog.pg_roles r CROSS JOIN pg_catalog.pg_attribute a
JOIN pg_catalog.pg_class c ON c.oid=a.attrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
CROSS JOIN (VALUES ('SELECT'),('INSERT'),('UPDATE'),('REFERENCES')) p(name)
WHERE r.rolname IN ('core_identity','core_tenant','core_platform','exec_identity',
 'exec_tenant','exec_bootstrap','exec_context','exec_integrity','g1_probe','ddl_owner','core_migrator')
 AND (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema')
 AND c.relkind IN ('r','p') AND a.attnum>0 AND NOT a.attisdropped;

SELECT json_build_object('kind','function','identity',p.oid::regprocedure::text,
 'schema',n.nspname,'name',p.proname,'owner',pg_get_userbyid(p.proowner),
 'securityDefiner',p.prosecdef,'volatility',p.provolatile,
 'leakproof',p.proleakproof,'config',p.proconfig,
 'bodyFingerprint',md5(pg_get_functiondef(p.oid)),
 'acl',coalesce((SELECT json_agg(json_build_object('grantee',
 CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END,
 'privilege',a.privilege_type,'grantable',a.is_grantable))
 FROM aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) a),'[]'::json))::text
FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace
WHERE p.prokind IN ('f','p') AND
 ((n.nspname !~ '^pg_' AND n.nspname <> 'information_schema')
 OR pg_get_userbyid(p.proowner) IN
 ('exec_identity','exec_tenant','exec_bootstrap','exec_context','exec_integrity','g1_probe','ddl_owner','core_migrator'));

SELECT json_build_object('kind','effectiveExecute','role',r.rolname,
 'function',p.oid::regprocedure::text,
 'allowed',has_function_privilege(r.oid,p.oid,'EXECUTE'))::text
FROM pg_catalog.pg_roles r CROSS JOIN pg_catalog.pg_proc p
JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace
WHERE r.rolname IN ('core_identity','core_tenant','core_platform','exec_identity',
 'exec_tenant','exec_bootstrap','exec_context','exec_integrity','g1_probe','ddl_owner','core_migrator')
 AND p.prokind IN ('f','p') AND
 ((n.nspname !~ '^pg_' AND n.nspname <> 'information_schema')
 OR pg_get_userbyid(p.proowner) IN
 ('exec_identity','exec_tenant','exec_bootstrap','exec_context','exec_integrity','g1_probe','ddl_owner','core_migrator'));

SELECT json_build_object('kind','policy','table',n.nspname||'.'||c.relname,
 'name',p.polname,'command',p.polcmd,'permissive',p.polpermissive,
 'roles',p.polroles::text,'using',pg_get_expr(p.polqual,p.polrelid),
 'withCheck',pg_get_expr(p.polwithcheck,p.polrelid))::text
FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid=p.polrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
WHERE (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema');

SELECT json_build_object('kind','trigger','table',n.nspname||'.'||c.relname,
 'name',t.tgname,'internal',t.tgisinternal,'enabled',t.tgenabled,
 'deferrable',t.tgdeferrable,'initiallyDeferred',t.tginitdeferred,
 'function',t.tgfoid::regprocedure::text,'definition',pg_get_triggerdef(t.oid))::text
FROM pg_catalog.pg_trigger t JOIN pg_catalog.pg_class c ON c.oid=t.tgrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
WHERE (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema');

SELECT json_build_object('kind','constraint','table',n.nspname||'.'||c.relname,
 'name',k.conname,'type',k.contype,'deferrable',k.condeferrable,
 'initiallyDeferred',k.condeferred,'validated',k.convalidated,
 'definition',pg_get_constraintdef(k.oid))::text
FROM pg_catalog.pg_constraint k JOIN pg_catalog.pg_class c ON c.oid=k.conrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
WHERE (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema');

SELECT json_build_object('kind','index','table',n.nspname||'.'||c.relname,
 'name',i.indexrelid::regclass::text,'unique',i.indisunique,
 'valid',i.indisvalid,'definition',pg_get_indexdef(i.indexrelid))::text
FROM pg_catalog.pg_index i JOIN pg_catalog.pg_class c ON c.oid=i.indrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
WHERE (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema');

SELECT json_build_object('kind','defaultAcl','owner',pg_get_userbyid(d.defaclrole),
 'schema',n.nspname,'objectType',d.defaclobjtype,'acl',d.defaclacl::text)::text
FROM pg_catalog.pg_default_acl d LEFT JOIN pg_catalog.pg_namespace n ON n.oid=d.defaclnamespace
WHERE pg_get_userbyid(d.defaclrole) IN
 ('exec_identity','exec_tenant','exec_bootstrap','exec_context','exec_integrity','ddl_owner','core_migrator')
 OR (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema');

-- Catalog dependencies are only a lower bound: PL/pgSQL/dynamic bodies need human review.
SELECT json_build_object('kind','catalogFunctionEdge','caller',src.oid::regprocedure::text,
 'callee',dst.oid::regprocedure::text,'dependencyType',d.deptype,
 'completeGraph',false)::text
FROM pg_catalog.pg_depend d
JOIN pg_catalog.pg_proc src ON d.classid='pg_proc'::regclass AND d.objid=src.oid
JOIN pg_catalog.pg_proc dst ON d.refclassid='pg_proc'::regclass AND d.refobjid=dst.oid
JOIN pg_catalog.pg_namespace n ON n.oid=src.pronamespace
WHERE (n.nspname !~ '^pg_' AND n.nspname <> 'information_schema')
 OR pg_get_userbyid(src.proowner) IN
 ('exec_identity','exec_tenant','exec_bootstrap','exec_context','exec_integrity','g1_probe','ddl_owner','core_migrator');

SELECT json_build_object('kind','effectiveSchemaPrivilege','role',r.rolname,
 'schema',n.nspname,'usage',has_schema_privilege(r.oid,n.oid,'USAGE'),
 'create',has_schema_privilege(r.oid,n.oid,'CREATE'))::text
FROM pg_catalog.pg_roles r CROSS JOIN pg_catalog.pg_namespace n
WHERE r.rolname IN ('core_identity','core_tenant','core_platform','exec_identity',
 'exec_tenant','exec_bootstrap','exec_context','exec_integrity','g1_probe','ddl_owner','core_migrator')
 AND n.nspname !~ '^pg_' AND n.nspname <> 'information_schema';

SELECT json_build_object('kind','database','name',d.datname,
 'owner',pg_get_userbyid(d.datdba),'acl',d.datacl::text)::text
FROM pg_catalog.pg_database d WHERE d.datname=current_database();

ROLLBACK;
