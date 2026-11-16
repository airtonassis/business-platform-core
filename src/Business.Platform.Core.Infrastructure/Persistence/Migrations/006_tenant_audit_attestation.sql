-- No secret or runtime login in migrations. Provision keys separately through an authorized operator.
CREATE SCHEMA bpc_crypto AUTHORIZATION bpc_e1_ddl;
REVOKE ALL ON SCHEMA bpc_crypto FROM PUBLIC;
CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA bpc_crypto;
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_extension e JOIN pg_namespace n ON n.oid=e.extnamespace WHERE e.extname='pgcrypto' AND n.nspname='bpc_crypto')
 THEN RAISE EXCEPTION 'pgcrypto schema requires explicit reconciliation'; END IF;
END $$;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA bpc_crypto FROM PUBLIC;
GRANT USAGE ON SCHEMA bpc_crypto TO bpc_e1_context_exec;
GRANT EXECUTE ON FUNCTION bpc_crypto.hmac(bytea,bytea,text) TO bpc_e1_context_exec;

CREATE TABLE bpc_security.audit_verification_keys(
 kid text PRIMARY KEY CHECK(kid ~ '^[A-Za-z0-9_-]{1,64}$'),
 secret bytea NOT NULL CHECK(octet_length(secret)=32),
 state text NOT NULL CHECK(state IN ('Active','Previous','Revoked')),
 retired_at timestamptz, previous_until timestamptz,
 CHECK(state<>'Previous' OR (retired_at IS NOT NULL AND previous_until IS NOT NULL
  AND isfinite(retired_at) AND isfinite(previous_until) AND previous_until>=retired_at
  AND previous_until<=retired_at+interval '32 seconds')));
ALTER TABLE bpc_security.audit_verification_keys OWNER TO bpc_e1_ddl;
ALTER TABLE bpc_security.audit_verification_keys ENABLE ROW LEVEL SECURITY;
ALTER TABLE bpc_security.audit_verification_keys FORCE ROW LEVEL SECURITY;
REVOKE ALL ON bpc_security.audit_verification_keys FROM PUBLIC;
GRANT SELECT(kid,secret,state,previous_until) ON bpc_security.audit_verification_keys TO bpc_e1_context_exec;
CREATE POLICY audit_key_verifier ON bpc_security.audit_verification_keys FOR SELECT TO bpc_e1_context_exec USING(true);

-- Internal verifier only. No signing/key-return function is exposed, even to the audit login.
CREATE FUNCTION bpc_security.verify_audit_attestation(p_payload text,p_mac bytea) RETURNS text[]
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE f text[]; key_bytes bytea; expected bytea; difference integer:=0; i integer;
 issued bigint; expires bigint; now_at numeric;
BEGIN
 IF p_payload IS NULL OR octet_length(p_payload)>8192 OR p_mac IS NULL OR octet_length(p_mac)<>32 THEN
  RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
 f:=string_to_array(p_payload,'.');
 IF cardinality(f)<>10 OR f[1]<>'v1' OR f[2] !~ '^[A-Za-z0-9_-]{1,64}$' THEN
  RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
 SELECT k.secret INTO key_bytes FROM bpc_security.audit_verification_keys k WHERE k.kid=f[2]
 AND (k.state='Active' OR k.state='Previous' AND clock_timestamp()<k.previous_until);
 IF key_bytes IS NULL THEN RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
 expected:=bpc_crypto.hmac(convert_to(p_payload,'UTF8'),key_bytes,'sha256');
 -- Fixed 32-byte comparison, no data-dependent early return on a mismatching byte.
 FOR i IN 0..31 LOOP difference:=difference | (get_byte(expected,i) # get_byte(p_mac,i)); END LOOP;
 IF difference<>0 THEN RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
 IF f[3] !~ '^[A-Za-z0-9_-]+$' OR f[4] !~ '^[A-Za-z0-9_-]+$'
 OR f[5] !~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
 OR f[6]<>'tenant-audit-read' OR f[7] !~ '^[1-9][0-9]{0,2}$' OR f[7]::int>100
 OR (f[8]<>'~' AND f[8] !~ '^[A-Za-z0-9_-]{1,128}$')
 OR f[9] !~ '^(0|[1-9][0-9]{0,11})$' OR f[10] !~ '^(0|[1-9][0-9]{0,11})$'
 THEN RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
 issued:=f[9]::bigint; expires:=f[10]::bigint; now_at:=extract(epoch FROM clock_timestamp());
 -- TTL exactly 30 seconds. At most 2 seconds future issuance. NO expiry grace.
 IF expires-issued<>30 OR issued>now_at+2 OR now_at>=expires THEN
  RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
 RETURN f;
END $$;
ALTER FUNCTION bpc_security.verify_audit_attestation(text,bytea) OWNER TO bpc_e1_context_exec;
REVOKE ALL ON FUNCTION bpc_security.verify_audit_attestation(text,bytea) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION bpc_security.verify_audit_attestation(text,bytea) TO bpc_e1_tenant_exec;

-- DROP removes ALL old ACLs, including previous audit-login grants. No executable fallback remains.
DROP FUNCTION bpc_security.read_tenant_audit(text,text,integer,timestamptz,uuid);
CREATE FUNCTION bpc_security.read_tenant_audit(p_payload text,p_mac bytea)
RETURNS TABLE(event_id uuid,occurred_at timestamptz,kind text,target_account_id uuid)
LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE f text[]; issuer text; subject text; context jsonb; org uuid; position text[];
 before_time timestamptz; before_id uuid; ticks bigint; raw_cursor text; canonical text;
BEGIN
 f:=bpc_security.verify_audit_attestation(p_payload,p_mac);
 issuer:=convert_from(decode(translate(f[3],'-_','+/')||repeat('=',(4-length(f[3])%4)%4),'base64'),'UTF8');
 subject:=convert_from(decode(translate(f[4],'-_','+/')||repeat('=',(4-length(f[4])%4)%4),'base64'),'UTF8');
 IF issuer='' OR subject='' OR octet_length(issuer)>8192 OR octet_length(subject)>2048 THEN
  RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
 IF rtrim(translate(replace(encode(convert_to(issuer,'UTF8'),'base64'),chr(10),''),'+/','-_'),'=')<>f[3]
 OR rtrim(translate(replace(encode(convert_to(subject,'UTF8'),'base64'),chr(10),''),'+/','-_'),'=')<>f[4] THEN
  RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
 context:=bpc_security.resolve_context(issuer,subject);
 IF context->>'state' IS DISTINCT FROM 'Linked' THEN RAISE EXCEPTION USING ERRCODE='P1404',MESSAGE='Access unavailable'; END IF;
 IF context->>'role' IS NULL OR context->>'role' NOT IN ('Owner','Admin') THEN RAISE EXCEPTION USING ERRCODE='P1403',MESSAGE='Access unavailable'; END IF;
 org:=(context->>'organizationId')::uuid;
 IF org IS NULL OR org::text<>f[5] THEN RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
 IF f[8]<>'~' THEN
  raw_cursor:=convert_from(decode(translate(f[8],'-_','+/')||repeat('=',(4-length(f[8])%4)%4),'base64'),'UTF8');
  position:=string_to_array(raw_cursor,'|');
  IF cardinality(position)<>3 OR position[1]<>'1' OR position[2] !~ '^(0|[1-9][0-9]{0,18})$' THEN
   RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
  ticks:=position[2]::bigint; before_id:=position[3]::uuid;
  IF ticks NOT BETWEEN 0 AND 3155378975999999999 OR ticks%10<>0 OR before_id='00000000-0000-0000-0000-000000000000'::uuid THEN
   RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
  canonical:=rtrim(translate(replace(encode(convert_to('1|'||ticks::text||'|'||before_id::text,'UTF8'),'base64'),chr(10),''),'+/','-_'),'=');
  IF canonical<>f[8] THEN RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable'; END IF;
  before_time:=timestamptz 'epoch' + ((ticks-621355968000000000)/10000000)*interval '1 second'
   + (((ticks-621355968000000000)%10000000)/10)*interval '1 microsecond';
 END IF;
 PERFORM set_config('app.organization_id',org::text,true);
 PERFORM set_config('app.account_id',context->>'accountId',true);
 RETURN QUERY SELECT e.event_id,e.occurred_at,e.kind,e.target_account_id FROM bpc_security.events e
 WHERE e.organization_id=org AND e.kind IN ('OrganizationBootstrapped','MembershipRevoked')
 AND (before_time IS NULL OR (e.occurred_at,e.event_id)<(before_time,before_id))
 ORDER BY e.occurred_at DESC,e.event_id DESC LIMIT f[7]::int+1;
EXCEPTION WHEN invalid_text_representation OR numeric_value_out_of_range OR character_not_in_repertoire
 THEN RAISE EXCEPTION USING ERRCODE='P1601',MESSAGE='Audit attestation unavailable';
END $$;
ALTER FUNCTION bpc_security.read_tenant_audit(text,bytea) OWNER TO bpc_e1_tenant_exec;
REVOKE ALL ON FUNCTION bpc_security.read_tenant_audit(text,bytea) FROM PUBLIC;

CREATE OR REPLACE FUNCTION bpc_security.persistence_ready() RETURNS boolean LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
 SELECT (SELECT array_agg(version ORDER BY version)=ARRAY[1,2,3,4,5,6] AND bool_and(length(checksum)=64) FROM bpc_migrations.versions)
 AND (SELECT count(*)=13 AND bool_and(c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='bpc_e1_ddl') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security') AND c.relkind='r')
 AND (SELECT count(*)=6 AND bool_and(NOT rolsuper AND NOT rolbypassrls AND NOT rolcanlogin AND NOT rolcreaterole AND NOT rolcreatedb) FROM pg_roles WHERE rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.roleid WHERE r.rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec'))
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace CROSS JOIN LATERAL aclexplode(coalesce(p.proacl,acldefault('f',p.proowner))) a WHERE n.nspname IN ('bpc_security','bpc_crypto') AND a.grantee=0 AND a.privilege_type='EXECUTE')
 AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='bpc_security' AND (p.prosecdef AND pg_get_userbyid(p.proowner)='bpc_e1_ddl' OR NOT coalesce(p.proconfig @> ARRAY['search_path=pg_catalog, pg_temp'],false)))
 AND to_regprocedure('bpc_security.read_tenant_audit(text,text,integer,timestamptz,uuid)') IS NULL
$$;
