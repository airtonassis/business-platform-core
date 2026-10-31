
-- Minimal replacement port required by K05; no emission UI/API is implemented.
GRANT SELECT(id,organization_id,recipient_identity_id,issuer_membership_id,status,expires_at,version) ON tenancy.invitations TO exec_tenant;
GRANT SELECT ON tenancy.invitation_slots,identity.identity_consumptions TO exec_tenant;
GRANT INSERT ON tenancy.invitations,tenancy.invitation_slots TO exec_tenant;
GRANT UPDATE(status,version,successor_id) ON tenancy.invitations TO exec_tenant;
GRANT UPDATE(version) ON tenancy.invitation_slots TO exec_tenant;
CREATE POLICY tenant_invitation ON tenancy.invitations TO exec_tenant
 USING(organization_id=authz.ctx('app.organization_id')) WITH CHECK(organization_id=authz.ctx('app.organization_id'));
CREATE POLICY tenant_slot ON tenancy.invitation_slots TO exec_tenant
 USING(organization_id=authz.ctx('app.organization_id')) WITH CHECK(organization_id=authz.ctx('app.organization_id'));
CREATE POLICY tenant_recipient_history ON identity.identity_consumptions FOR SELECT TO exec_tenant
 USING(identity_id=authz.ctx('app.recipient_id'));

CREATE FUNCTION operation.replace_invitation(inv uuid, expected_version bigint, request_key text, new_secret text)
RETURNS uuid LANGUAGE plpgsql VOLATILE SECURITY DEFINER SET search_path=pg_catalog,pg_temp AS $$
DECLARE item record; receipt record;
 actor uuid:=authz.ctx('app.identity_id'); org uuid:=authz.ctx('app.organization_id'); next_id uuid:=gen_random_uuid(); issuer_id uuid;
BEGIN
 IF session_user<>'core_tenant' OR NOT authz.own_tenant_allowed(true) OR request_key IS NULL OR length(request_key)=0
 THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 IF expected_version IS NULL THEN RAISE EXCEPTION USING ERRCODE='P2006',MESSAGE='Version.Required'; END IF;
 IF new_secret IS NULL OR length(new_secret)<64 THEN RAISE EXCEPTION USING ERRCODE='22023',MESSAGE='Invalid generated secret'; END IF;
 INSERT INTO operation.idempotency_records(actor_id,scope_key,operation,key,request_hash,result_id)
 VALUES(actor,org::text,'replace',request_key,inv::text||':'||expected_version::text,next_id)
 ON CONFLICT(actor_id,scope_key,operation,key) DO NOTHING;
 SELECT actor_id,scope_key,operation,key,request_hash,result_id INTO receipt FROM operation.idempotency_records
 WHERE actor_id=actor AND scope_key=org::text AND operation='replace' AND key=request_key;
 IF receipt.request_hash<>inv::text||':'||expected_version::text
 THEN RAISE EXCEPTION USING ERRCODE='P2001',MESSAGE='Idempotency.PayloadMismatch'; END IF;
 IF EXISTS(SELECT FROM tenancy.invitations WHERE id=receipt.result_id) THEN RETURN receipt.result_id; END IF;
 SELECT id,organization_id,recipient_identity_id,issuer_membership_id,status,expires_at,version INTO item FROM tenancy.invitations WHERE id=inv AND organization_id=org;
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='P2003',MESSAGE='Invitation.Unavailable'; END IF;
 PERFORM set_config('app.recipient_id',item.recipient_identity_id::text,true);
 PERFORM id FROM identity.identities WHERE id=ANY(ARRAY[actor,item.recipient_identity_id]) ORDER BY id FOR UPDATE;
 PERFORM id FROM tenancy.organizations WHERE id=org FOR UPDATE;
 PERFORM id FROM identity.enterprise_user_accounts WHERE id=authz.ctx('app.account_id') FOR UPDATE;
 PERFORM id FROM tenancy.memberships WHERE account_id=authz.ctx('app.account_id') ORDER BY id FOR UPDATE;
 IF NOT authz.own_tenant_allowed(true) THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 SELECT id INTO issuer_id FROM tenancy.memberships WHERE account_id=authz.ctx('app.account_id') AND organization_id=org AND status='Active' AND role IN('Owner','Admin');
 IF NOT FOUND THEN RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Access.Denied'; END IF;
 IF NOT EXISTS(SELECT FROM identity.identities WHERE id=item.recipient_identity_id AND status='Active')
 OR EXISTS(SELECT FROM identity.identity_consumptions WHERE identity_id=item.recipient_identity_id)
 THEN RAISE EXCEPTION USING ERRCODE='P2002',MESSAGE='Account.Ineligible'; END IF;
 INSERT INTO tenancy.invitation_slots(organization_id,recipient_identity_id) VALUES(org,item.recipient_identity_id) ON CONFLICT DO NOTHING;
 PERFORM version FROM tenancy.invitation_slots WHERE organization_id=org AND recipient_identity_id=item.recipient_identity_id FOR UPDATE;
 SELECT id,organization_id,recipient_identity_id,issuer_membership_id,status,expires_at,version INTO item FROM tenancy.invitations WHERE id=inv FOR UPDATE;
 IF item.version<>expected_version THEN RAISE EXCEPTION USING ERRCODE='P2007',MESSAGE='Version.Stale'; END IF;
 IF item.status NOT IN('Pending','Expired') THEN RAISE EXCEPTION USING ERRCODE='P2004',MESSAGE='Invitation.StateConflict'; END IF;
 IF item.status='Pending' AND item.expires_at<=clock_timestamp() THEN
  UPDATE tenancy.invitations SET status='Expired',version=version+1 WHERE id=inv;
  INSERT INTO audit.events(organization_id,actor_id,action,operation_id) VALUES(org,actor,'ExpireInvitation',gen_random_uuid());
 END IF;
 PERFORM set_config('app.integrity_org',org::text,true);
 PERFORM set_config('app.integrity_tx',pg_current_xact_id()::text,true);
 PERFORM set_config('app.operation_id',gen_random_uuid()::text,true);
 -- Clear the partial Pending index before inserting successor; no temporary broken FK.
 UPDATE tenancy.invitations SET status='Replaced',version=version+1 WHERE id=inv;
 INSERT INTO tenancy.invitations(id,organization_id,recipient_identity_id,issuer_membership_id,secret_hash,status,expires_at,predecessor_id)
 VALUES(next_id,org,item.recipient_identity_id,issuer_id,sha256(convert_to(new_secret,'UTF8')),'Pending',clock_timestamp()+interval '24 hours',inv);
 UPDATE tenancy.invitations SET successor_id=next_id,version=version+1 WHERE id=inv;
 INSERT INTO audit.events(organization_id,actor_id,action,operation_id) VALUES(org,actor,'ReplaceInvitation',gen_random_uuid());
 RETURN next_id;
END $$;
ALTER FUNCTION operation.replace_invitation(uuid,bigint,text,text) OWNER TO exec_tenant;
REVOKE ALL ON FUNCTION operation.replace_invitation(uuid,bigint,text,text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION operation.replace_invitation(uuid,bigint,text,text) TO core_tenant;
