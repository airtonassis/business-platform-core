"""Offline, non-approving comparison of the captured disposable PostgreSQL catalog.
Usage: python harness/compare_observed.py <run-id>. Never overwrites evidence.
"""
import hashlib
import json
import re
import sys
from pathlib import Path

package = Path(__file__).resolve().parent.parent
run = sys.argv[1]
revision = sys.argv[2] if len(sys.argv) > 2 else ""
if revision and not re.fullmatch(r"[a-z0-9-]+", revision):
    raise ValueError("Invalid evidence revision")
if not re.fullmatch(r"[0-9a-f]{12}", run):
    raise ValueError("Invalid run identifier")
folder = package / "evidence" / "runs" / run
expected_path = folder / "preflight.expected.json"
if not expected_path.exists():
    raise ValueError("Pre-observation expectation is mandatory; never derive expected from an observation")
expected = json.loads(expected_path.read_text(encoding="utf-8-sig"))
rows = [json.loads(line) for line in (folder / "catalog.jsonl").read_text(encoding="utf-8-sig").splitlines()]
bodies = json.loads((folder / "graph.json").read_text(encoding="utf-8-sig"))

def kind(name):
    return [r for r in rows if r["kind"] == name]

def save(name, value):
    if revision:
        name = revision + "." + name
    with (folder / name).open("x", encoding="utf-8") as target:
        json.dump(value, target, indent=2, ensure_ascii=False)
        target.write("\n")

save("security.expected.snapshot.json", expected)

functions = kind("function")
tables = kind("table")
memberships = kind("membership") + kind("roleMembership")
groups = []
for owner in expected["owners"]:
    role = owner["name"]
    frozen = {(g["kind"], g["table"], g["column"], g["privilege"]) for g in owner["effectiveGrants"]}
    actual = {("table", r["table"], "", r["privilege"]) for r in kind("effectiveTablePrivilege") if r["role"] == role and r["allowed"]}
    actual |= {("column", r["table"], r["column"], r["privilege"]) for r in kind("effectiveColumnPrivilege") if r["role"] == role and r["allowed"]}
    actual_execute = {r["function"] for r in kind("effectiveExecute") if r["role"] == role and r["allowed"]}
    expected_execute = set(owner["expectedExecute"])
    actual_usage = {r["schema"] for r in kind("effectiveSchemaPrivilege") if r["role"] == role and r["usage"]}
    unexpected_create = [r for r in kind("effectiveSchemaPrivilege") if r["role"] == role and r["create"]]
    role_observed = next(r for r in kind("role") if r["name"] == role)
    attribute_mismatches = {k: {"expected": owner[k], "observed": role_observed[v]} for k,v in {"login":"canLogin", "superuser":"superuser", "bypassRls":"bypassRls", "inherit":"inherit"}.items() if owner[k] != role_observed[v]}
    groups.append({"owner": role,
        "expectedAttributes": {key: owner[key] for key in ("login", "superuser", "bypassRls", "inherit")},
        "attributes": [r for r in kind("role") if r["name"] == role],
        "functions": [r for r in functions if r["owner"] == role],
        "tablePrivileges": [r for r in kind("effectiveTablePrivilege") if r["role"] == role and r["allowed"]],
        "columnPrivileges": [r for r in kind("effectiveColumnPrivilege") if r["role"] == role and r["allowed"]],
        "schemaPrivileges": [r for r in kind("effectiveSchemaPrivilege") if r["role"] == role and (r["usage"] or r["create"])],
        "execute": [r for r in kind("effectiveExecute") if r["role"] == role and r["allowed"]],
        "exactGrantComparison": "UNDETERMINED: expected effectiveGrants is not frozen" if not frozen else ("MATCH_TEST_ALLOWLIST_NOT_HUMAN_APPROVAL" if frozen == actual else "DIVERGENT"),
        "missingGrants": sorted(frozen - actual) if frozen else None,
        "excessGrants": sorted(actual - frozen) if frozen else None,
        "executeMissing": sorted(expected_execute - actual_execute),
        "executeExcess": sorted(actual_execute - expected_execute),
        "schemaUsageMissing": sorted(set(owner["expectedSchemaUsage"]) - actual_usage),
        "schemaUsageExcess": sorted(actual_usage - set(owner["expectedSchemaUsage"])),
        "unexpectedSchemaCreate": unexpected_create,
        "functionOwnershipMissing": sorted(set(owner["exactFunctionSignatures"]) - {f["identity"] for f in functions if f["owner"] == role}),
        "functionOwnershipExcess": sorted({f["identity"] for f in functions if f["owner"] == role} - set(owner["exactFunctionSignatures"])),
        "attributeMismatches": attribute_mismatches,
        "candidateFunctionsNotLiteralBindings": owner["candidateFunctions"]})
observed = {"run": run, "status": "OBSERVED_SELF_REVIEW_NOT_INDEPENDENT_APPROVAL",
    "catalogSha256": hashlib.sha256((folder / "catalog.jsonl").read_bytes()).hexdigest(),
    "groups": groups, "memberships": memberships, "tables": tables,
    "effectiveExecute": kind("effectiveExecute"),
    "directFunctionAcl": [{"function": f["identity"], "acl": f.get("acl")} for f in functions],
    "directTableAcl": [{"table": t["name"], "acl": t.get("acl")} for t in tables],
    "columnAclEvidence": "catalog.jsonl effectiveColumnPrivilege.declaredAcl (includes false rows)",
    "policies": kind("policy"), "defaultAcl": kind("defaultAcl"),
    "review": "PENDING_INDEPENDENT_HUMAN_REVIEW"}
save("security.observed.json", observed)
graph = []
names = {f["identity"].split("(")[0] for f in functions}
for body in bodies:
    # Inspect only the body, excluding the CREATE FUNCTION declaration itself.
    definition = body["definition"].split("$function$")[1]
    calls = sorted(n for n in names if re.search(r"\b" + re.escape(n) + r"\s*\(", definition))
    graph.append({"signature": body["signature"], "owner": body["owner"], "calls": calls,
        "dynamicSqlDetected": bool(re.search(r"\bEXECUTE\s+(?!PROCEDURE|FUNCTION)", definition, re.I)),
        "definitionSha256": hashlib.sha256(body["definition"].encode()).hexdigest()})
implicit = []
for trigger in kind("trigger"):
    implicit.append({"from": trigger["table"] + ":" + trigger["name"], "to": trigger["function"], "type": "trigger/internal-FK" if trigger["internal"] else "trigger/validator", "deferrable": trigger["deferrable"], "definition": trigger["definition"], "execution": "SECURITY DEFINER switches to function owner; INVOKER keeps triggering context, deferred INVOKER would use outer runtime at COMMIT"})
for policy in kind("policy"):
    for name in names:
        if re.search(r"\b" + re.escape(name) + r"\s*\(", (policy.get("using") or "") + " " + (policy.get("withCheck") or "")):
            implicit.append({"from": policy["table"] + ":" + policy["name"], "to": name, "type": "RLS-policy", "roles": policy["roles"], "execution": "Policy caller context, then callee SECURITY DEFINER if declared"})
for body in bodies:
    definition = body["definition"].split("$function$")[1]
    for table in tables:
        if re.search(r"\b" + re.escape(table["name"]) + r"\b", definition):
            implicit.append({"from": body["signature"], "to": table["name"], "type": "body-relation-reference", "execution": "SECURITY DEFINER owner or INVOKER caller, then table RLS/policies; lexical reference requires semantic review"})
ownership = [{"owner": f["owner"], "object": f["identity"], "privilege": "Owner can administer function and intrinsically execute; EXECUTE ACL observed separately", "securityDefiner": f["securityDefiner"]} for f in functions]
ownership += [{"owner": t["owner"], "object": t["name"], "privilege": "Table owner can alter/disable RLS/triggers; FORCE does not constrain owner DDL. Never a runtime capability."} for t in tables]
save("graph.observed.json", {"run": run, "status": "EXPLICIT_FIXED_CALLS_AND_CATALOG_IMPLICIT_EDGES_PENDING_REVIEW",
    "functions": graph, "implicitEdges": implicit, "catalogFunctionEdges": kind("catalogFunctionEdge"), "ownershipCapabilities": ownership, "inheritanceEdges": memberships,
    "limitation": "Lexical bodies and catalog edges require human semantic review, including system functions and trusted DDL owner. No claim of independent approval.",
    "completePrivilegeGraphApproved": False, "independentReview": "PENDING"})
public_execute = [{"function": f["identity"], "grant": a} for f in functions for a in (f.get("acl") or []) if a["grantee"] == "PUBLIC" and a["privilege"] == "EXECUTE"]
unsafe_roles = [r for r in kind("role") if r["name"] != "postgres" and any(r.get(k, False) for k in ("superuser", "bypassRls", "createRole", "createDb", "replication"))]
runtime = {"core_identity": {"operation.accept_invitation"}, "core_tenant": {"operation.update_demo", "operation.revoke_membership", "operation.replace_invitation"}, "core_platform": {"operation.bootstrap"}}
unexpected_execute = [{"function": f["identity"], "grant": a} for f in functions for a in (f.get("acl") or []) if a["grantee"] in runtime and f["identity"].split("(")[0] not in runtime[a["grantee"]] | {"authz.ctx", "authz.own_tenant_allowed"}]
save("security.comparison.json", {"run": run, "expectedSha256": hashlib.sha256(expected_path.read_bytes()).hexdigest(),
    "status": "PARTIAL_OBJECTIVE_COMPARISON_NOT_G1_APPROVAL", "ownerGroups": [{"owner": g["owner"], "functionCount": len(g["functions"]), "effectiveTableCount": len(g["tablePrivileges"]), "effectiveColumnCount": len(g["columnPrivileges"]), "exactGrantComparison": g["exactGrantComparison"]} for g in groups],
    "missingExpectedTables": sorted({t["name"] for t in expected["tables"]} - {t["name"] for t in tables}),
    "additionalObservedTables": sorted({t["name"] for t in tables} - {t["name"] for t in expected["tables"]}),
    "publicExecute": public_execute, "unexpectedRuntimeExecuteAgainstImplementedPorts": unexpected_execute,
    "unsafeRoleAttributes": unsafe_roles, "memberships": memberships,
    "rlsOrOwnershipViolations": [t for t in tables if not t["rls"] or not t["forceRls"] or t["owner"] != "ddl_owner"],
    "missingOrExcessExactGrants": [{"owner": g["owner"], "missing": g["missingGrants"], "excess": g["excessGrants"], "executeMissing": g["executeMissing"], "executeExcess": g["executeExcess"], "schemaUsageMissing": g["schemaUsageMissing"], "schemaUsageExcess": g["schemaUsageExcess"], "unexpectedSchemaCreate": g["unexpectedSchemaCreate"], "functionOwnershipMissing": g["functionOwnershipMissing"], "functionOwnershipExcess": g["functionOwnershipExcess"]} for g in groups],
    "approvedGraphComparison": "UNDETERMINED: approvedEdges is empty; fixed-call extraction excludes implicit trigger/policy/system-function edges",
    "D1": "SQL credential plus fully forged context is accepted residual trust boundary, not a demonstrated legitimate-Application breach",
    "ownerAttributeMismatches": [{"owner": g["owner"], "mismatches": g["attributeMismatches"]} for g in groups],
    "functionModeMismatches": [{"function": f["identity"], "expectedSecurityDefiner": expected["functionModes"][f["identity"]], "observedSecurityDefiner": f["securityDefiner"]} for f in functions if f["identity"] in expected.get("functionModes", {}) and f["securityDefiner"] != expected["functionModes"][f["identity"]]],
    "independentReview": "PENDING"})
# Offline sensitivity check only: frozen expectation is never changed, and no SQL
# privilege is granted. Show that the comparator reports both forbidden addition
# and missing required capability rather than normalizing them into expected.
tenant = next(g for g in groups if g["owner"] == "exec_tenant")
frozen_tenant = {(g["kind"],g["table"],g["column"],g["privilege"]) for g in next(o for o in expected["owners"] if o["name"] == "exec_tenant")["effectiveGrants"]}
forbidden = ("column", "tenancy.invitations", "secret_hash", "SELECT")
required = ("column", "tenancy.memberships", "role", "SELECT")
assert forbidden not in frozen_tenant and required in frozen_tenant
save("comparison-sensitivity.json", {"run": run, "mode": "OFFLINE_COMPARATOR_SENSITIVITY_NOT_DATABASE_AUTHORIZATION_TEST",
    "hypotheticalUnexpectedPrivilegeDetected": sorted((frozen_tenant | {forbidden}) - frozen_tenant),
    "hypotheticalMissingPrivilegeDetected": sorted(frozen_tenant - (frozen_tenant - {required})),
    "expectedOrDatabaseModified": False})
print(json.dumps({"run": run, "owners": len(groups), "tables": len(tables), "functions": len(functions), "publicExecute": len(public_execute), "unexpectedRuntimeExecute": len(unexpected_execute), "unsafeRoles": len(unsafe_roles)}))
