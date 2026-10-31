"""Author a candidate from a manual operation capability model, before observation.
No database, observed evidence, GRANT parser or catalog metadata is an input.
SQL grants and the expectation are both implementations of this reviewable intent;
agreement verifies installation/defaults, not correctness of the intent itself.
"""
import datetime
import hashlib
import json
from pathlib import Path

p = Path(__file__).resolve().parent.parent
root = p.parents[2]
model = {}

def capability(owner, table, privilege, columns, reason):
    for column in columns.split():
        model.setdefault(owner, []).append(dict(kind="column", table=table,
            column=column, privilege=privilege, justification=reason + "; column=" + column))

def cap(owner, table, select="", insert="", update="", reason=""):
    for privilege, cols in (("SELECT", select), ("INSERT", insert), ("UPDATE", update)):
        capability(owner, table, privilege, cols, reason)

audit = "organization_id actor_id action operation_id"
receipt = "actor_id scope_key operation key request_hash result_id"
for role in ("exec_identity", "exec_tenant", "exec_bootstrap"):
    cap(role, "identity.identities", "id status", update="version", reason="ADR-0009 C1; command actor/recipient eligibility predicates and Identity FOR UPDATE; version UPDATE permits row lock, not a lifecycle port")
    cap(role, "audit.events", insert=audit, reason="C06/C09; command appends scoped actor/action/operation atomically; id/time default, no read/update/delete")
for role in ("exec_tenant", "exec_bootstrap"):
    cap(role, "operation.idempotency_records", receipt, receipt, reason="C06; exact actor/scope/operation/key predicate and payload/result receipt, INSERT ON CONFLICT, no receipt id needed")
for role in ("exec_identity", "exec_tenant"):
    cap(role, "tenancy.organizations", "id status" if role == "exec_identity" else "id", update="version", reason="ADR-0009 C1; lock organization by id; accept also checks Active; UPDATE version needed for FOR UPDATE")
    cap(role, "tenancy.invitation_slots", "organization_id recipient_identity_id version", "organization_id recipient_identity_id", "version", "C04/C1; unique recipient slot insert and row lock/version predicate")
    cap(role, "identity.identity_consumptions", "identity_id", reason="ADR-0008 lifetime consumption; C04 reject historical recipient, existence predicate by identity only")
cap("exec_identity", "identity.identity_consumptions", insert="identity_id organization_id account_id", reason="ADR-0008/C04 accept records permanent binding atomically")
cap("exec_identity", "identity.enterprise_user_accounts", "id identity_id organization_id status", "id identity_id organization_id status", reason="C04 accept INSERT and authorized Accepted replay; invitation guard verifies account recipient/org/Active; version default, no account lock/update")
cap("exec_identity", "tenancy.memberships", "account_id status", "id organization_id account_id role status", reason="C04 accept creates Member; replay checks active member by account; guards use row images; no role/status UPDATE")
cap("exec_identity", "tenancy.invitations", "id organization_id recipient_identity_id secret_hash status expires_at version accepted_account_id", update="status accepted_account_id version", reason="C04 secret/destination proof, fresh version/expiry after locks, Accepted account result; chain validator reads under separate exec_integrity owner; no invite INSERT or metadata edits")
cap("exec_tenant", "identity.enterprise_user_accounts", "id identity_id version", update="status version", reason="C08/C09 revoke exact member's account and actor account locks by id/identity_id; version UPDATE requires SELECT version; authorization reads under exec_context, accepted-account check runs only on identity accept path; terminal trigger uses row images")
cap("exec_tenant", "tenancy.memberships", "id organization_id account_id role status version", update="status version", reason="C08 revoke role/version predicates, actor locks and issuer binding; no UPDATE role, Owner revocation denied by trigger")
cap("exec_tenant", "sandbox.demo_records", "id organization_id version", update="title version", reason="C08 demonstrative update selects exact id/org/version, no creation, no title read needed by this port")
cap("exec_tenant", "tenancy.invitations", "id organization_id recipient_identity_id issuer_membership_id status expires_at version predecessor_id successor_id", "id organization_id recipient_identity_id issuer_membership_id secret_hash status expires_at predecessor_id", "status version successor_id", "C04 replace only: metadata and locked predecessor; INSERT new Pending hash, no accepted_account_id/successor/version/created_at input; trigger guards actor, eligibility and reciprocal chain; no SELECT hash")
cap("exec_bootstrap", "identity.identity_consumptions", "identity_id", "identity_id organization_id account_id", reason="ADR-0008/C09 bootstrap eligibility and permanent consumption INSERT")
cap("exec_bootstrap", "identity.recipient_references", "identity_id secret_hash expires_at", reason="C02/C09 bootstrap verifies exact owner reference hash and expiry")
cap("exec_bootstrap", "tenancy.organizations", "id", "id name status", reason="C09 bootstrap creates org and checks prior receipt result exists; no org UPDATE")
cap("exec_bootstrap", "identity.enterprise_user_accounts", insert="id identity_id organization_id status", reason="C09 bootstrap account id generated locally; no SELECT/UPDATE required, validator uses separate context")
cap("exec_bootstrap", "tenancy.memberships", insert="id organization_id account_id role status", reason="C09 bootstrap initial Owner INSERT; no SELECT/UPDATE, role immutable afterward")
for role in ("exec_context", "exec_integrity"):
    cap(role, "tenancy.organizations", "id status", reason="ADR-0009 technical Active anchor; integrity reads affected org independent of actor")
    cap(role, "identity.enterprise_user_accounts", "id identity_id organization_id status" if role == "exec_context" else "id organization_id status", reason="ADR-0009 context tuple resolution or final Active Account/Member invariant; no mutation")
    cap(role, "tenancy.memberships", "account_id organization_id role status", reason="ADR-0009 actor authorization or final Owner/member invariant; no mutation")
cap("exec_context", "identity.identities", "id status", reason="ADR-0009 T/SELF: own_tenant_allowed joins Identity by id and requires Active; no identity mutation")
cap("exec_integrity", "tenancy.invitations", "id organization_id recipient_identity_id status predecessor_id successor_id", reason="C04 reciprocal replacement invariant at COMMIT; deferred validator must run in technical context, not outer runtime role; requires scoped RLS and explicit org/transaction anchor")

signatures = {
    "exec_identity": ["operation.accept_invitation(uuid,text,bigint)"],
    "exec_tenant": ["operation.update_demo(uuid,bigint,text)", "operation.revoke_membership(uuid,bigint)", "operation.replace_invitation(uuid,bigint,text,text)"],
    "exec_bootstrap": ["operation.bootstrap(uuid,uuid,text,text,text)"],
    "exec_context": ["authz.own_tenant_allowed(boolean)"],
    "exec_integrity": ["authz.validate_integrity()", "authz.guard_immutable()", "authz.guard_invitation()", "authz.validate_invitation_chain()"]}
schemas = {"exec_identity": ["authz", "identity", "tenancy", "audit"],
    "exec_tenant": ["authz", "identity", "tenancy", "audit", "operation", "sandbox"],
    "exec_bootstrap": ["authz", "identity", "tenancy", "audit", "operation"],
    "exec_context": ["authz", "identity", "tenancy"], "exec_integrity": ["authz", "identity", "tenancy"]}
target = p / "manifests/security.expected.json"
old = target.read_bytes()
archive = p / "manifests/history" / ("baseline-" + hashlib.sha256(old).hexdigest() + ".json")
archive.parent.mkdir(exist_ok=True)
if not archive.exists():
    archive.write_bytes(old)
expected = {
    "formatVersion": 2,
    "status": "PRE_OBSERVATION_MANUAL_CAPABILITY_CANDIDATE_PENDING_INDEPENDENT_APPROVAL",
    "decisions": {"D1": "APPROVED_TRUST_BOUNDARY_RESIDUAL_UNCHANGED", "D2": "APPROVED_FIVE_GROUPS_CANDIDATE_CAPABILITIES_REQUIRE_INDEPENDENT_APPROVAL"},
    "schemas": ["identity", "tenancy", "sandbox", "audit", "operation", "authz"],
    "runtimeLogins": ["core_identity", "core_tenant", "core_platform"],
    "tables": [{"name": table, "rlsRequired": True, "forceRlsRequired": True} for table in sorted({g["table"] for grants in model.values() for g in grants})],
    "owners": [{"name": role, "login": False, "superuser": False, "bypassRls": False, "inherit": False, "tableOwnershipAllowed": False,
        "candidateFunctions": signatures[role]} for role in signatures],
    "callGraph": {"status": "CANDIDATE_REQUIRES_INDEPENDENT_REVIEW", "approvedEdges": []},
    "functionModes": {signature: (signature not in ["authz.guard_immutable()", "authz.guard_invitation()"]) for functions in signatures.values() for signature in functions},
    "helperFunction": {"signature": "authz.ctx(text)", "owner": "ddl_owner", "securityDefiner": False},
    "controls": {"publicExecuteAllowed": False, "membershipOrSetRoleToOwnersAllowed": False,
        "runtimeDdlAllowed": False, "schemaCreateByExecutorsAllowed": False},
    "note": "Constructed entirely from manual contract/operation model; old file only archived, never parsed as expectation input."}
for owner in expected["owners"]:
    owner["effectiveGrants"] = sorted(model[owner["name"]], key=lambda g: (g["table"], g["column"], g["privilege"]))
    owner["exactFunctionSignatures"] = signatures[owner["name"]]
    owner["grantsStatus"] = "MANUAL_OPERATION_INTENT_NOT_OBSERVED_OR_DDL_EXTRACTION"
    owner["expectedSchemaUsage"] = schemas[owner["name"]] + ["public"]
    owner["schemaJustification"] = "USAGE only for qualified relations/helpers accessed by these bodies; public USAGE comes from standard PUBLIC ACL, never CREATE"
    owner["expectedExecute"] = signatures[owner["name"]] + ["authz.ctx(text)"] + (["authz.own_tenant_allowed(boolean)"] if owner["name"] == "exec_tenant" else [])
    owner["executeJustification"] = "Owner intrinsically executes own functions; explicit ctx helper grant; tenant invokes authorization helper. No other execution allowed in authz/operation."
expected["allowlistScope"] = {
    "frozenAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "basis": "Manual per-operation capabilities with per-privilege justification; no observed evidence or SQL GRANT parsing input",
    "authoringScriptSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    "approvedSources": {str(f.relative_to(root)): hashlib.sha256(f.read_bytes()).hexdigest() for f in [root / "docs/architecture/decisions/ADR-0008-organization-membership-and-context.md", root / "docs/architecture/decisions/ADR-0009-persistence-isolation-and-concurrency.md", root / "docs/architecture/SPRINT_1_CONTRATOS_FLUXOS.md"]},
    "status": "CANDIDATE_NOT_APPROVED_BY_EXECUTOR", "historicalBaseline": str(archive.relative_to(p)),
    "scope": "All table/column privileges for five owners; schema/EXECUTE/ownership/inheritance checked separately"}
target.write_text(json.dumps(expected, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
sql = ["-- TEST ONLY. Install manual operation capability candidate; no observed input."]
tables = sorted({g["table"] for grants in model.values() for g in grants})
for role, grants in model.items():
    sql.append(f"REVOKE USAGE ON SCHEMA authz,identity,tenancy,audit,operation,sandbox FROM {role};")
    sql.append(f"GRANT USAGE ON SCHEMA {','.join(schemas[role])} TO {role};")
    for table in tables:
        sql.append(f"REVOKE ALL ON TABLE {table} FROM {role};")
        # REVOKE ALL on table does not revoke prior explicit column grants.
        for privilege in ("SELECT", "INSERT", "UPDATE", "REFERENCES"):
            # Revoke existing declared column grants using fixed SQL source relation
            # metadata at install time; never feeds expected values.
            sql.append("DO $revoke$ DECLARE cols text; BEGIN SELECT string_agg(quote_ident(attname),',') INTO cols FROM pg_catalog.pg_attribute WHERE attrelid='"+table+"'::regclass AND attnum>0 AND NOT attisdropped; EXECUTE 'REVOKE "+privilege+"('||cols||') ON "+table+" FROM "+role+"'; END $revoke$;")
    grouped = {}
    for g in grants:
        grouped.setdefault((g["table"], g["privilege"]), set()).add(g["column"])
    for (table, privilege), cols in sorted(grouped.items()):
        sql.append(f"GRANT {privilege}({','.join(sorted(cols))}) ON {table} TO {role};")
(p / "runtime/004-candidate-grants.sql").write_text("\n".join(sql) + "\n", encoding="utf-8")
print(json.dumps({"status": expected["status"], "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "counts": {r: len(g) for r,g in model.items()}}))
