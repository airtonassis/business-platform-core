"""Read-only G2 artifact validation. Only Git subprocesses; no historical runner imports."""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit

SPEC = importlib.util.spec_from_file_location(
    "g1_offline", Path(__file__).resolve().parent.parent / "ADR0009.G1.CI/validate_g1.py")
g1 = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(g1)
digest, load_json = g1.digest, g1.load_json
G1 = "tests/TechnicalValidation/ADR0009.G1"
G2 = "tests/TechnicalValidation/ADR0009.G2"
SUPPORT = "tests/TechnicalValidation/ADR0009.G2.CI"
WORKFLOW = ".github/workflows/adr0009-g2.yml"
G1_LOCK = "tests/TechnicalValidation/ADR0009.G1.CI/originals.sha256.json"
RUNS = ("f31a6617ed85", "6909108391b4")
ALLOWED_IDS = {"Q07", "Q08", "Q11", "Q12", "V01", "V02", "V03", "V04", "V05",
               "V06", "V07", "V08", "F01", "F02", "F03", "F04", "F05", "F06", "F09", "H01"}
# Published bd7963a anchors: byte integrity only, not a new SQL privilege allowlist.
ANCHORS = {
    G2 + "/evidence.sha256.json": "6b004116a7a905615380aef93bfb27fba150d999c44fb75142d282a2ac106353",
    G2 + "/regularizacao.sha256.json": "613abe424819bfa06e46366b3465e7f89ac1380dd0182a92e9f662720cbefc07",
    G2 + "/evidence/.gitattributes": "22a1d3278ef23d6aa9ed7e530168f85bf1c740996ce9ab2897ee5fdfe07c3006",
    "tests/TechnicalValidation/ADR0009.G1.CI/originals.sha256.json": "e7c0c9a217534b2d22f7661a31f0dba6bc51d0df9df43d280122711ffb9dc83e",
}


def require(ok, message):
    if not ok:
        raise ValueError(message)


def file(root, name):
    p = g1.safe_path(root, name)
    require(p.is_file() and not p.is_symlink(), "REFERENCE: missing/unsafe file: " + name)
    return p


def check_hashes(root, entries):
    require(isinstance(entries, dict), "HASH: invalid manifest structure")
    for name, expected in entries.items():
        p = file(root, name)
        require(isinstance(expected, str) and re.fullmatch(r"[a-fA-F0-9]{64}", expected), "HASH: invalid digest")
        observed = digest(p)
        if observed != expected.lower() and name.replace("\\", "/") == G1_LOCK:
            # Only this non-evidence support file is Git-normalized (* text=auto).
            # The historical G2 source reference hashes its Windows CRLF bytes.
            # Verify those exact recorded bytes in memory; never rewrite the file,
            # change the recorded checksum or normalize any G1/G2 evidence.
            data = p.read_bytes()
            if b"\r" not in data:
                observed = hashlib.sha256(data.replace(b"\n", b"\r\n")).hexdigest()
        require(observed == expected.lower(), "HASH: mismatch: " + name)


def pointer(value, path):
    require(isinstance(path, str) and path.startswith("/"), "REFERENCE: invalid JSON pointer")
    try:
        for part in path[1:].split("/"):
            require(not re.search(r"~(?![01])", part), "REFERENCE: invalid pointer escape")
            key = part.replace("~1", "/").replace("~0", "~")
            if isinstance(value, list):
                require(bool(re.fullmatch(r"0|[1-9][0-9]*", key)), "REFERENCE: invalid array index")
                value = value[int(key)]
            else:
                value = value[key]
    except (KeyError, IndexError, TypeError):
        raise ValueError("REFERENCE: unresolved JSON pointer") from None
    return value


def reference(root, ref):
    return pointer(load_json(file(root, ref["path"])), ref["jsonPointer"])


def check_candidate(name):
    p = Path(name.replace("\\", "/"))
    require(not set(p.parts) & g1.GENERATED_DIRS, "GENERATED: forbidden directory")
    if p.suffix.lower() == ".log":
        require(bool(re.fullmatch(r"evidence/runs/[0-9a-f]{12}/postgres\.log", p.as_posix())), "GENERATED: unrelated log")
    else:
        require(p.suffix.lower() not in g1.GENERATED_SUFFIXES | {".tmp", ".bak", ".zip"}, "GENERATED: forbidden artifact")


def inspect_text(root, path):
    text = path.read_text(encoding="utf-8-sig")
    if path.suffix == ".json":
        g1.parse_json(text)
    elif path.suffix == ".jsonl":
        for line in text.splitlines():
            g1.parse_json(line)
    for rule, pattern in g1.SECRETS.items():
        require(not re.search(pattern, text), "SECRET: " + path.name + ": " + rule + " (redacted)")
    if path.suffix == ".md":
        for target in re.findall(r"\]\(([^)]+)\)", text):
            target = target.strip("<>").replace("\\", "/")
            if urlsplit(target).scheme:
                continue
            resolved = (path.parent / unquote(target.split("#")[0])).resolve()
            require(resolved.is_relative_to(root.resolve()) and resolved.is_file(), "REFERENCE: missing/escaping Markdown link")


def pairs(entries, id_key="id"):
    out = [(x[id_key], x["variant"]) for x in entries]
    require(len(out) == len(set(out)), "COVERAGE: duplicate variant")
    return set(out)


def validate_coverage(root):
    package = root / G2
    c = load_json(package / "coverage.json")
    declared = pairs(c["variants"])
    require(len(declared) == 31 and all(i in ALLOWED_IDS for i, _ in declared), "COVERAGE: G2 scope/count")
    require(c["selectedCounts"] == {"PASS": 31}, "COVERAGE: declared result counts")
    require(c["excludedCounts"] == {"NOT RUN": 7, "DEFERRED": 13}, "COVERAGE: historical exclusion counts")
    require(Counter(x["result"] for x in c["excluded"]) == c["excludedCounts"], "COVERAGE: exclusion mismatch")
    require(set(c["runs"]) == set(RUNS), "COVERAGE: unexpected run")
    executed = {}
    for run, count in zip(RUNS, (30, 1)):
        folder = package / "evidence/runs" / run
        result = load_json(folder / "results.json")
        entries = result["subproofs"]
        selected = load_json(folder / "selection.json")["selected"]
        require(len(entries) == count and pairs(entries, "caseId") == pairs(selected), "COVERAGE: run/selection mismatch")
        require(result["schemaInstalled"] and not result["stoppedForP0"], "COVERAGE: final run stopped")
        for p in entries:
            key = p["caseId"], p["variant"]
            require(key not in executed and p["status"] == "PASS", "COVERAGE: duplicate/incompatible run result")
            executed[key] = run
    require(set(executed) == declared, "COVERAGE: missing/excess executed variant")
    require(all(x["run"] == executed[(x["id"], x["variant"])] and x["result"] == "PASS"
                for x in c["variants"]), "COVERAGE: result reference mismatch")
    for x in c["variants"]:
        for key in ("evidence", "assertions"):
            file(package, x[key])
    return c


def validate_traceability(root):
    package = root / G2
    c = load_json(package / "coverage.json")
    t = load_json(package / "RASTREABILIDADE_COMPLEMENTAR.json")
    require(pairs(t["variants"]) == pairs(c["variants"]) and len(t["variants"]) == 31, "TRACE: variant set")
    bindings = 0
    coverage_pointers = set()
    for x in t["variants"]:
        require(x["coveragePointer"] not in coverage_pointers, "TRACE: duplicate coverage pointer")
        coverage_pointers.add(x["coveragePointer"])
        original = pointer(c, x["coveragePointer"])
        require(all(x[k] == original[k] for k in ("id", "variant", "run", "result")), "TRACE: coverage mismatch")
        p = reference(root, x["resultReference"])
        require((p["caseId"], p["variant"], p["status"]) == (x["id"], x["variant"], "PASS"), "TRACE: result mismatch")
        assertions = load_json(file(root, x["assertionReference"]["path"]))
        require(bool(x["assertionReference"]["jsonPointers"]), "TRACE: empty assertions")
        for where in x["assertionReference"]["jsonPointers"]:
            a = pointer(assertions, where)
            require(a["proof"] == x["id"] + "/" + x["variant"], "TRACE: unrelated assertion")
            require(a.get("observed") is not False and a.get("matched") is not False and a.get("check") != "ORIGIN_MISMATCH", "TRACE: failed assertion")
        timeline = reference(root, x["timelineReference"])
        require(isinstance(timeline, list) and x["timelineReference"]["proofFilter"] == x["id"] + "/" + x["variant"], "TRACE: timeline reference")
        require(bool(x["requirements"]), "TRACE: empty requirement")
        for b in x["requirements"]:
            require(reference(root, b["scenarioSource"]) == b["protectionAndTraceability"], "TRACE: requirement mismatch")
            ref = b["currentPlan"]
            lines = file(root, ref["path"]).read_text(encoding="utf-8").splitlines()
            require(1 <= ref["line"] <= len(lines) and lines[ref["line"] - 1] == "#### " + ref["heading"], "TRACE: plan heading")
            require(ref["heading"].startswith(b["protocolId"] + " "), "TRACE: protocol heading mismatch")
            bindings += 1
    require(bindings == 37, "TRACE: expected 37 existing requirement bindings")
    return t


def validate_reservations(root, t):
    package = root / G2
    c = load_json(package / "coverage.json")
    v = t["V09Erratum"]
    require(pointer(c, v["historicalPointer"])["id"] == "V09", "P2: V09 exclusion reference")
    approved = load_json(file(root, v["approvedEvidence"]["path"]))
    require(any(x["caseId"] == "V09" and x["variant"] == v["variant"] and x["status"] == "PASS"
                for x in approved["subproofs"]), "P2: approved V09/G1 missing")
    addendum = t["tableInventoryAddendum"]
    expected = load_json(file(root, addendum["frozenExpectedSource"]["path"]))
    original = {x["name"] for x in expected["tables"]}
    require(len(original) == 11 and "identity.identity_links" not in original, "P2: original omission changed")
    require(set(addendum["documentaryExpectedNames"]) == original | {"identity.identity_links"}, "P2: table addendum")
    require(len(addendum["observedCorrespondence"]) == 2, "P2: table observations")
    for ref in addendum["observedCorrespondence"]:
        lines = file(root, ref["path"]).read_text(encoding="utf-8").splitlines()
        row = g1.parse_json(lines[ref["jsonlLine"] - 1])
        require(row["name"] == "identity.identity_links" and row["owner"] == "ddl_owner" and row["rls"] and row["forceRls"], "P2: table observation mismatch")
    failures = []
    for run in sorted((package / "evidence/runs").iterdir()):
        require(run.is_dir(), "HISTORY: unexpected run artifact")
        r = load_json(run / "results.json")
        failures += [(run.name, x["caseId"]) for x in r["subproofs"] if x["status"] == "FAIL"]
        cleanup = load_json(run / "cleanup.json")
        require(cleanup["cleanup"] == "REMOVED_RUN_CONTAINER_AND_TMPFS" and cleanup["networkRemoved"] and cleanup["remaining"] == "", "HISTORY: cleanup record")
    require(sorted(failures) == sorted([("3ed87d94f5a1", "V02"), ("e76986da515a", "V08"), ("5c56afedf9e5", "Q12")]), "HISTORY: FAIL records changed")
    report = (package / "REGISTRO_APROVACAO_E_RESSALVAS.md").read_text(encoding="utf-8")
    for text in ("G2 APROVADO COM RESSALVAS", "As quatro ressalvas P2 foram aceitas", "diagnóstico nativo anterior à assertion não foi capturado", "foi inferida", "Data da decisão humana", "não informada", "não autoriza G3, G4, Sprint 1"):
        require(text in report, "P2: missing approval/limitation record")
    for run in RUNS:
        folder = package / "evidence/runs" / run
        env = load_json(folder / "environment.json")
        require("PostgreSQL 17.11" in env["postgres"], "HISTORY: database version record")
        for name in ("isolation-verified.json", "cleanup.json", "postgres.log"):
            file(folder, name)


def validate_workflow(root):
    text = file(root, WORKFLOW).read_text(encoding="utf-8")
    require("pull_request:" in text and "runs-on: ubuntu-24.04" in text, "WORKFLOW: event/runner")
    require("permissions:\n  contents: read" in text and text.count("permissions:") == 1, "WORKFLOW: read-only permissions")
    require("persist-credentials: false" in text, "WORKFLOW: checkout credentials")
    require(not re.search(r"pull_request_target|secrets[.:]|services:|container:|write-all|contents: write|id-token\x3a", text), "WORKFLOW: unsafe capability")
    actions = re.findall(r"uses:\s*(\S+)", text)
    require(actions == ["actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803"], "WORKFLOW: unpinned/unexpected action")
    commands = re.findall(r"^\s+(?:-\s+)?run:\s*(.+)$", text, re.MULTILINE)
    require(commands == [
        "python3 -B -m unittest discover -s tests/TechnicalValidation/ADR0009.G2.CI -p test_validate_g2.py -v",
        "python3 -B tests/TechnicalValidation/ADR0009.G2.CI/validate_g2.py",
        "git diff --exit-code -- tests/TechnicalValidation/ADR0009.G1 tests/TechnicalValidation/ADR0009.G2",
        "git diff --check origin/main...HEAD"], "WORKFLOW: unexpected execution step")


def validate(root):
    check_hashes(root, ANCHORS)
    package = root / G2
    historical = load_json(package / "evidence.sha256.json")["files"]
    require(len(historical) == 177, "INVENTORY: historical manifest entry count")
    check_hashes(package, historical)
    complement = load_json(package / "regularizacao.sha256.json")
    check_hashes(package, {complement["historicalManifest"]["path"]: complement["historicalManifest"]["sha256"]})
    check_hashes(package, complement["addedFiles"])
    check_hashes(root, complement["sourceDocuments"])
    originals = load_json(file(root, "tests/TechnicalValidation/ADR0009.G1.CI/originals.sha256.json"))["files"]
    require(len(originals) == 272, "G1: original count")
    check_hashes(root / G1, originals)
    # The G1 verifier is offline and read-only; never import G2 historical scripts.
    require(not g1.validate(root), "G1: offline validation violation")
    files = subprocess.check_output(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", G2 + "/", SUPPORT + "/", WORKFLOW], cwd=root).decode().split("\0")
    files = sorted(set(x for x in files if x))
    expected = set(historical) | set(complement["addedFiles"]) | {"evidence.sha256.json", "regularizacao.sha256.json", "evidence/.gitattributes"}
    actual = {x[len(G2) + 1:] for x in files if x.startswith(G2 + "/")}
    require(actual == expected, "INVENTORY: missing/unexpected G2 file")
    for name in files:
        relative = name[len(G2) + 1:] if name.startswith(G2 + "/") else name
        check_candidate(relative)
        inspect_text(root, file(root, name))
    attrs = subprocess.check_output(["git", "check-attr", "-z", "--stdin", "text"], cwd=root,
                                    input=b"".join((G2 + "/" + x).encode() + b"\0" for x in sorted(actual))).decode().split("\0")
    require(all(attrs[i] == "unset" for i in range(2, len(attrs) - 1, 3)), "ATTRIBUTES: G2 files require -text")
    validate_coverage(root)
    trace = validate_traceability(root)
    validate_reservations(root, trace)
    validate_workflow(root)
    return {"historicalG2Entries": 177, "G2VersionedCandidates": len(actual), "G1Originals": 272,
            "historicalPassVariants": 31, "traceabilityBindings": 37, "historicalFAILs": 3,
            "mode": "OFFLINE_READ_ONLY_NO_DATABASE_NO_HARNESS_NO_GATE_APPROVAL"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    try:
        print("PASS: " + json.dumps(validate(args.root.resolve())))
    except (ValueError, KeyError, TypeError, IndexError, OSError, subprocess.CalledProcessError) as error:
        message = str(error) if isinstance(error, ValueError) else "invalid input or unavailable Git metadata"
        print("FAIL: " + message, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
