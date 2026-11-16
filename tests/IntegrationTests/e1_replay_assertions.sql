-- Regression: an idempotency receipt must not bypass current identity revocation/blocking.
BEGIN;
DO $$ DECLARE r record; BEGIN
 SELECT * INTO r FROM bpc_security.operation_receipts LIMIT 1;
 IF NOT FOUND THEN RAISE EXCEPTION 'Controlled bootstrap fixture required'; END IF;
 UPDATE identity.identities SET status='Blocked' WHERE identity_id=r.target_identity_id;
 BEGIN
  PERFORM bpc_security.bootstrap_organization(r.target_identity_id,r.organization_name,r.operation_id);
  RAISE EXCEPTION 'Replay bypassed current identity status';
 EXCEPTION WHEN SQLSTATE 'P1001' THEN NULL;
 END;
END $$;
ROLLBACK;
