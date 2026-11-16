-- Product E1: revalidate current identity under lock before idempotent receipt replay.
CREATE OR REPLACE FUNCTION bpc_security.bootstrap_organization(p_identity uuid,p_name text,p_operation uuid) RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE r record; org uuid:=gen_random_uuid(); acc uuid:=gen_random_uuid(); mem uuid:=gen_random_uuid(); st text;
BEGIN
 PERFORM set_config('app.identity_id',p_identity::text,true); PERFORM set_config('app.operation_id',p_operation::text,true);
 PERFORM pg_advisory_xact_lock(hashtextextended(p_operation::text,1));
 SELECT status INTO st FROM identity.identities WHERE identity_id=p_identity FOR UPDATE;
 IF st IS DISTINCT FROM 'Active' THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Access unavailable'; END IF;

 SELECT operation_id,target_identity_id,organization_id,account_id,membership_id,organization_name INTO r FROM bpc_security.operation_receipts WHERE operation_id=p_operation;
 IF FOUND THEN
  IF r.target_identity_id<>p_identity OR r.organization_name<>p_name THEN RAISE EXCEPTION USING ERRCODE='P1001',MESSAGE='Operation conflict'; END IF;
  RETURN jsonb_build_object('organizationId',r.organization_id,'accountId',r.account_id,'membershipId',r.membership_id);
 END IF;
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
