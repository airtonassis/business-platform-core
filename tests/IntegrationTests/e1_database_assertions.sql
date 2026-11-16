-- Synthetic fixtures, only within migrator dry-run transaction; rolled back entirely.
CREATE ROLE bpc_e1_probe NOLOGIN NOSUPERUSER NOBYPASSRLS NOINHERIT;
GRANT USAGE ON SCHEMA bpc_security TO bpc_e1_probe;
GRANT EXECUTE ON FUNCTION bpc_security.register_identity(text,text),bpc_security.resolve_context(text,text) TO bpc_e1_probe;
SET LOCAL ROLE bpc_e1_probe;
SELECT bpc_security.register_identity('https://synthetic.invalid','subject-A') AS id_a \gset
SELECT bpc_security.register_identity('https://synthetic.invalid','subject-B') AS id_b \gset
DO $$ BEGIN IF bpc_security.resolve_context('https://synthetic.invalid','subject-A')->>'state'<>'NotLinked' THEN RAISE EXCEPTION 'NotLinked failed'; END IF; END $$;
RESET ROLE;
GRANT EXECUTE ON FUNCTION bpc_security.bootstrap_organization(uuid,text,uuid) TO bpc_e1_probe;
SET LOCAL ROLE bpc_e1_probe;
SELECT bpc_security.bootstrap_organization(:'id_a','Synthetic A','aaaaaaaa-0000-0000-0000-000000000001')->>'organizationId' AS org_a \gset
SELECT bpc_security.bootstrap_organization(:'id_b','Synthetic B','bbbbbbbb-0000-0000-0000-000000000001')->>'organizationId' AS org_b \gset
DO $$ BEGIN
 IF bpc_security.resolve_context('https://synthetic.invalid','subject-A')->>'state'<>'Linked' THEN RAISE EXCEPTION 'Linked failed'; END IF;
 BEGIN
  UPDATE tenancy.memberships SET status='Revoked',version=version+1;
  RAISE EXCEPTION 'Direct DML unexpectedly allowed';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
 BEGIN
  CREATE TABLE tenancy.invitations(id uuid);
  RAISE EXCEPTION 'DDL unexpectedly allowed';
 EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END $$;
RESET ROLE;
SET CONSTRAINTS ALL IMMEDIATE;
DO $$ DECLARE t record; BEGIN
 FOR t IN SELECT c.relname,c.relrowsecurity,c.relforcerowsecurity FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('identity','tenancy','bpc_security') AND c.relkind='r' LOOP
  IF NOT t.relrowsecurity OR NOT t.relforcerowsecurity THEN RAISE EXCEPTION 'RLS missing'; END IF;
 END LOOP;
 IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname IN ('bpc_e1_ddl','bpc_e1_identity_exec','bpc_e1_tenant_exec','bpc_e1_boot_exec','bpc_e1_integrity_exec','bpc_e1_context_exec') AND (rolsuper OR rolbypassrls OR rolcanlogin)) THEN RAISE EXCEPTION 'Unsafe role'; END IF;
END $$;
SELECT 'registration,NotLinked,bootstrap,Linked,direct-DML-denied,invitation-DDL-denied,deferred-integrity,RLS,owners:PASS';
SET CONSTRAINTS ALL DEFERRED;
-- P1-01 guard independent of ACL, even multiple Owners (privileged synthetic fixture).
DO $$ DECLARE m record; a uuid:=gen_random_uuid(); i uuid:=gen_random_uuid(); mem uuid:=gen_random_uuid(); BEGIN
 SELECT * INTO m FROM tenancy.memberships LIMIT 1;
 INSERT INTO identity.identities(identity_id,status) VALUES(i,'Active');
 INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id,origin) VALUES(i,m.organization_id,a,'PlatformBootstrap');
 INSERT INTO identity.enterprise_user_accounts(account_id,identity_id,organization_id,status) VALUES(a,i,m.organization_id,'Active');
 INSERT INTO tenancy.memberships(membership_id,organization_id,account_id,status,role) VALUES(mem,m.organization_id,a,'Active','Owner');
 BEGIN UPDATE tenancy.memberships SET status='Revoked',version=version+1 WHERE membership_id=m.membership_id; RAISE EXCEPTION 'Owner bypass'; EXCEPTION WHEN SQLSTATE 'P1001' THEN NULL; END;
END $$;
-- Active non-Owner used only as synthetic fixture, not an invitation implementation.
SET CONSTRAINTS ALL DEFERRED;
DO $$ DECLARE m record; a uuid:=gen_random_uuid(); i uuid:=gen_random_uuid(); mem uuid:=gen_random_uuid(); actor uuid; BEGIN
 SELECT * INTO m FROM tenancy.memberships LIMIT 1;
 SELECT identity_id INTO actor FROM identity.enterprise_user_accounts WHERE account_id=m.account_id;
 INSERT INTO identity.identities(identity_id,status) VALUES(i,'Active');
 INSERT INTO identity.identity_links(link_id,identity_id,issuer,subject) VALUES(gen_random_uuid(),i,'https://synthetic.invalid','member');
 INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id,origin) VALUES(i,m.organization_id,a,'PlatformBootstrap');
 INSERT INTO identity.enterprise_user_accounts(account_id,identity_id,organization_id,status) VALUES(a,i,m.organization_id,'Active');
 INSERT INTO tenancy.memberships(membership_id,organization_id,account_id,status,role) VALUES(mem,m.organization_id,a,'Active','Member');
 PERFORM bpc_security.revoke_membership(actor,m.organization_id,mem,1,gen_random_uuid());
 IF (SELECT status FROM identity.enterprise_user_accounts WHERE account_id=a)<>'Revoked' THEN RAISE EXCEPTION 'Atomic revoke failed'; END IF;
 IF bpc_security.resolve_context('https://synthetic.invalid','member')->>'state'<>'Denied' THEN RAISE EXCEPTION 'Revoked became NotLinked'; END IF;
 IF NOT EXISTS(SELECT 1 FROM identity.identity_consumptions WHERE identity_id=i) THEN RAISE EXCEPTION 'History lost'; END IF;
 BEGIN PERFORM bpc_security.bootstrap_organization(i,'Other',gen_random_uuid()); RAISE EXCEPTION 'Identity reused'; EXCEPTION WHEN SQLSTATE 'P1001' THEN NULL; END;
END $$;
SET CONSTRAINTS ALL IMMEDIATE;
SELECT 'multiple-Owner-guard,atomic-revoke,permanent-history,reuse-denied:PASS';
