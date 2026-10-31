-- Test-only narrow command ports, not production endpoints.
CREATE FUNCTION operation.bootstrap(owner_identity uuid, org uuid, org_name text, request_key text, owner_reference text)
RETURNS uuid LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE existing record; aid uuid:=gen_random_uuid(); mid uuid:=gen_random_uuid(); actor uuid:=authz.ctx('app.identity_id');
BEGIN
 IF session_user <> 'core_platform' OR actor IS NULL OR request_key IS NULL OR length(request_key)=0
 THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 PERFORM set_config('app.recipient_id',owner_identity::text,true);
 -- OIDC/MFA stays at the Application boundary; C02 hash proof is rechecked below.
 INSERT INTO operation.idempotency_records(actor_id,scope_key,operation,key,request_hash,result_id)
 VALUES(actor,'Platform','bootstrap',request_key,owner_identity::text||':'||org::text||':'||org_name,org)
 ON CONFLICT(actor_id,scope_key,operation,key) DO NOTHING;
 SELECT actor_id,scope_key,operation,key,request_hash,result_id INTO existing FROM operation.idempotency_records
 WHERE actor_id=actor AND scope_key='Platform' AND operation='bootstrap' AND key=request_key;
 IF existing.request_hash <> owner_identity::text||':'||org::text||':'||org_name
 THEN RAISE EXCEPTION USING ERRCODE='P2001',MESSAGE='Idempotency.PayloadMismatch'; END IF;
 -- A previous result exists only if the organization was committed.
 PERFORM set_config('app.integrity_org',org::text,true);
 IF EXISTS(SELECT FROM tenancy.organizations WHERE id=existing.result_id) THEN RETURN existing.result_id; END IF;
 PERFORM id FROM identity.identities WHERE id=ANY(ARRAY[actor,owner_identity]) ORDER BY id FOR UPDATE;
 IF NOT EXISTS(SELECT FROM identity.recipient_references WHERE identity_id=owner_identity
 AND secret_hash=sha256(convert_to(owner_reference,'UTF8')) AND expires_at>clock_timestamp())
 THEN RAISE EXCEPTION USING ERRCODE='P2003',MESSAGE='Recipient.Unavailable'; END IF;
 IF NOT EXISTS(SELECT FROM identity.identities WHERE id=owner_identity AND status='Active')
 OR EXISTS(SELECT FROM identity.identity_consumptions WHERE identity_id=owner_identity)
 THEN RAISE EXCEPTION USING ERRCODE='P2002',MESSAGE='Account.Ineligible'; END IF;
 PERFORM set_config('app.integrity_tx',pg_current_xact_id()::text,true);
 PERFORM set_config('app.operation_id',gen_random_uuid()::text,true);
 INSERT INTO tenancy.organizations(id,name,status) VALUES(org,org_name,'Active');
 INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id) VALUES(owner_identity,org,aid);
 INSERT INTO identity.enterprise_user_accounts(id,identity_id,organization_id,status) VALUES(aid,owner_identity,org,'Active');
 INSERT INTO tenancy.memberships(id,organization_id,account_id,role,status) VALUES(mid,org,aid,'Owner','Active');
 INSERT INTO audit.events(organization_id,actor_id,action,operation_id) VALUES(org,actor,'Bootstrap',authz.ctx('app.operation_id'));
 RETURN org;
END $$;
ALTER FUNCTION operation.bootstrap(uuid,uuid,text,text,text) OWNER TO exec_bootstrap;
REVOKE ALL ON FUNCTION operation.bootstrap(uuid,uuid,text,text,text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION operation.bootstrap(uuid,uuid,text,text,text) TO core_platform;

CREATE FUNCTION operation.accept_invitation(inv uuid, secret text, expected_version bigint)
RETURNS uuid LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE item record; aid uuid:=gen_random_uuid(); actor uuid:=authz.ctx('app.identity_id');
BEGIN
 IF session_user<>'core_identity' OR actor IS NULL THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 SELECT id,organization_id,recipient_identity_id,status,expires_at,version,accepted_account_id INTO item FROM tenancy.invitations WHERE id=inv AND recipient_identity_id=actor AND secret_hash=sha256(convert_to(secret,'UTF8'));
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P2003',MESSAGE='Invitation.Unavailable'; END IF;
 PERFORM id FROM identity.identities WHERE id=actor AND status='Active' FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 PERFORM set_config('app.organization_id',item.organization_id::text,true);
 PERFORM id FROM tenancy.organizations WHERE id=item.organization_id AND status='Active' FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 -- Revalidate in a new statement after the identity/org waits.
 SELECT id,organization_id,recipient_identity_id,status,expires_at,version,accepted_account_id INTO item FROM tenancy.invitations WHERE id=inv AND recipient_identity_id=actor
 AND secret_hash=sha256(convert_to(secret,'UTF8'));
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P2003',MESSAGE='Invitation.Unavailable'; END IF;
 IF item.status='Accepted' THEN
  SELECT id INTO aid FROM identity.enterprise_user_accounts WHERE id=item.accepted_account_id AND status='Active';
  IF aid IS NULL OR NOT EXISTS(SELECT FROM tenancy.memberships WHERE account_id=aid AND status='Active')
  THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Unavailable'; END IF;
  RETURN aid;
 END IF;
 IF EXISTS(SELECT FROM identity.identity_consumptions WHERE identity_id=actor) THEN
 RAISE EXCEPTION USING ERRCODE='P2002',MESSAGE='Account.Ineligible'; END IF;
 INSERT INTO tenancy.invitation_slots(organization_id,recipient_identity_id) VALUES(item.organization_id,actor) ON CONFLICT DO NOTHING;
 PERFORM version FROM tenancy.invitation_slots WHERE organization_id=item.organization_id AND recipient_identity_id=actor FOR UPDATE;
 SELECT id,organization_id,recipient_identity_id,status,expires_at,version,accepted_account_id INTO item FROM tenancy.invitations WHERE id=inv FOR UPDATE;
 IF item.status<>'Pending' THEN RAISE EXCEPTION USING ERRCODE='P2004',MESSAGE='Invitation.StateConflict'; END IF;
 IF item.expires_at<=clock_timestamp() THEN RAISE EXCEPTION USING ERRCODE='P2005',MESSAGE='Invitation.Expired'; END IF;
 IF expected_version IS NULL THEN RAISE EXCEPTION USING ERRCODE='P2006',MESSAGE='Version.Required'; END IF;
 IF item.version<>expected_version THEN RAISE EXCEPTION USING ERRCODE='P2007',MESSAGE='Version.Stale'; END IF;
 PERFORM set_config('app.integrity_org',item.organization_id::text,true);
 PERFORM set_config('app.integrity_tx',pg_current_xact_id()::text,true);
 PERFORM set_config('app.operation_id',gen_random_uuid()::text,true);
 INSERT INTO identity.identity_consumptions(identity_id,organization_id,account_id) VALUES(actor,item.organization_id,aid);
 INSERT INTO identity.enterprise_user_accounts(id,identity_id,organization_id,status) VALUES(aid,actor,item.organization_id,'Active');
 INSERT INTO tenancy.memberships(id,organization_id,account_id,role,status) VALUES(gen_random_uuid(),item.organization_id,aid,'Member','Active');
 UPDATE tenancy.invitations SET status='Accepted',accepted_account_id=aid,version=version+1 WHERE id=inv;
 INSERT INTO audit.events(organization_id,actor_id,action,operation_id) VALUES(item.organization_id,actor,'Accept',authz.ctx('app.operation_id'));
 RETURN aid;
END $$;
ALTER FUNCTION operation.accept_invitation(uuid,text,bigint) OWNER TO exec_identity;
REVOKE ALL ON FUNCTION operation.accept_invitation(uuid,text,bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION operation.accept_invitation(uuid,text,bigint) TO core_identity;

CREATE FUNCTION operation.update_demo(target uuid, expected_version bigint, new_title text)
RETURNS bigint LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE ver bigint; actor uuid:=authz.ctx('app.identity_id'); org uuid:=authz.ctx('app.organization_id');
BEGIN
 IF session_user<>'core_tenant' OR NOT authz.own_tenant_allowed(true)
 THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 PERFORM id FROM identity.identities WHERE id=actor FOR SHARE;
 PERFORM id FROM tenancy.organizations WHERE id=org FOR UPDATE;
 PERFORM id FROM identity.enterprise_user_accounts WHERE id=authz.ctx('app.account_id') FOR UPDATE;
 PERFORM id FROM tenancy.memberships WHERE account_id=authz.ctx('app.account_id') ORDER BY id FOR UPDATE;
 IF NOT authz.own_tenant_allowed(true) THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 SELECT version INTO ver FROM sandbox.demo_records WHERE id=target AND organization_id=org FOR UPDATE;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P2003',MESSAGE='Resource.Unavailable'; END IF;
 IF expected_version IS NULL THEN RAISE EXCEPTION USING ERRCODE='P2006',MESSAGE='Version.Required'; END IF;
 IF ver<>expected_version THEN RAISE EXCEPTION USING ERRCODE='P2007',MESSAGE='Version.Stale'; END IF;
 UPDATE sandbox.demo_records SET title=new_title,version=version+1 WHERE id=target RETURNING version INTO ver;
 INSERT INTO audit.events(organization_id,actor_id,action,operation_id) VALUES(org,actor,'UpdateDemo',gen_random_uuid());
 RETURN ver;
END $$;
ALTER FUNCTION operation.update_demo(uuid,bigint,text) OWNER TO exec_tenant;
REVOKE ALL ON FUNCTION operation.update_demo(uuid,bigint,text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION operation.update_demo(uuid,bigint,text) TO core_tenant;

CREATE FUNCTION operation.revoke_membership(target uuid, expected_version bigint)
RETURNS void LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE actor uuid:=authz.ctx('app.identity_id'); org uuid:=authz.ctx('app.organization_id');
 target_account uuid; target_identity uuid; target_role text; target_version bigint; actor_role text;
BEGIN
 IF session_user<>'core_tenant' OR NOT authz.own_tenant_allowed(true) THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 SELECT account_id INTO target_account FROM tenancy.memberships WHERE id=target AND organization_id=org;
 SELECT identity_id INTO target_identity FROM identity.enterprise_user_accounts WHERE id=target_account;
 IF target_identity IS NULL OR target_identity=actor THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 PERFORM set_config('app.recipient_id',target_identity::text,true);
 PERFORM id FROM identity.identities WHERE id=ANY(ARRAY[actor,target_identity]) ORDER BY id FOR UPDATE;
 PERFORM id FROM tenancy.organizations WHERE id=org FOR UPDATE;
 PERFORM id FROM identity.enterprise_user_accounts WHERE id=ANY(ARRAY[authz.ctx('app.account_id'),target_account]) ORDER BY id FOR UPDATE;
 PERFORM id FROM tenancy.memberships WHERE account_id=ANY(ARRAY[authz.ctx('app.account_id'),target_account]) ORDER BY id FOR UPDATE;
 IF NOT authz.own_tenant_allowed(true) THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 SELECT role INTO actor_role FROM tenancy.memberships WHERE account_id=authz.ctx('app.account_id') AND status='Active';
 SELECT role,version INTO target_role,target_version FROM tenancy.memberships WHERE id=target AND status='Active';
 IF target_role IS NULL OR target_role='Owner' OR (actor_role='Admin' AND target_role<>'Member')
 THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 IF expected_version IS NULL THEN RAISE EXCEPTION USING ERRCODE='P2006',MESSAGE='Version.Required'; END IF;
 IF target_version<>expected_version THEN RAISE EXCEPTION USING ERRCODE='P2007',MESSAGE='Version.Stale'; END IF;
 PERFORM set_config('app.integrity_org',org::text,true);
 PERFORM set_config('app.integrity_tx',pg_current_xact_id()::text,true);
 PERFORM set_config('app.operation_id',gen_random_uuid()::text,true);
 UPDATE identity.enterprise_user_accounts SET status='Revoked',version=version+1 WHERE id=target_account;
 UPDATE tenancy.memberships SET status='Revoked',version=version+1 WHERE id=target;
 INSERT INTO audit.events(organization_id,actor_id,action,operation_id) VALUES(org,actor,'Revoke',authz.ctx('app.operation_id'));
END $$;
ALTER FUNCTION operation.revoke_membership(uuid,bigint) OWNER TO exec_tenant;
REVOKE ALL ON FUNCTION operation.revoke_membership(uuid,bigint) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION operation.revoke_membership(uuid,bigint) TO core_tenant;
