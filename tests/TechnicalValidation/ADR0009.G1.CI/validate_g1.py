"""Offline integrity checks. Never imports/runs the SQL harness or rewrites evidence.

The separate originals lock preserves file bytes, not a new security allowlist.
Historical FAIL/BLOQUEADO/NOT_EXECUTED records are valid historical evidence.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit


PACKAGE = "tests/TechnicalValidation/ADR0009.G1"
LOCK = Path(__file__).with_name("originals.sha256.json")
BUILD_DIRS = {"bin", "obj"}
GENERATED_DIRS = BUILD_DIRS | {"__pycache__", "TestResults", "coverage"}
GENERATED_SUFFIXES = {".dll", ".pdb", ".exe", ".pyc", ".log", ".trx"}
SECRETS = {
    "private-key": r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----",
    "github-token": r"\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{30,})\b",
    "aws-key": r"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b",
    "jwt": r"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b",
    "service-token": r"\b(?:sk-(?:proj-)?[A-Za-z0-9_-]{20,}|xox[baprs]-[A-Za-z0-9-]{12,})\b",
    "credential-url": r"://[^\s/:]+:[^\s/@]+@",
    "literal-credential": r"(?i)(?:password|pwd|token|client_secret|api_key|apikey)\s*[=:]\s*[\x22\x27][^\x22\x27\r\n]{8,}[\x22\x27]",
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def safe_path(root, raw):
    """Normalize separators in memory; reject absolute/traversing manifest paths."""
    value = raw.replace("\\", "/")
    parts = PurePosixPath(value)
    if not value or parts.is_absolute() or ":" in value or ".." in parts.parts:
        raise ValueError("unsafe relative path")
    target = root.joinpath(*parts.parts)
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError("path escapes root")
    return target


def unique_object(pairs):
    obj = {}
    for key, value in pairs:
        if key in obj:
            raise ValueError("duplicate JSON key")
        obj[key] = value
    return obj


def reject_constant(value):
    raise ValueError("non-finite JSON number")


def parse_json(text):
    return json.loads(text, object_pairs_hook=unique_object, parse_constant=reject_constant)


def load_json(path):
    return parse_json(path.read_text(encoding="utf-8-sig"))


def generated(path):
    return bool(set(path.parts) & GENERATED_DIRS) or path.suffix.lower() in GENERATED_SUFFIXES


def validate_files(package, expected, candidates=None, reference_root=None):
    errors = []
    boundary = (reference_root or package).resolve()
    actual = {p.relative_to(package).as_posix(): p for p in package.rglob("*")
              if p.is_file() and not set(p.relative_to(package).parts) & BUILD_DIRS}
    for name in sorted(set(expected) - set(actual)):
        errors.append("MISSING: " + name)
    for name in sorted(set(actual) - set(expected)):
        errors.append("UNEXPECTED: " + name)
    for candidate in candidates or actual.values():
        if generated(candidate.relative_to(package)):
            errors.append("GENERATED: " + candidate.relative_to(package).as_posix())
    for name, checksum in expected.items():
        try:
            path = safe_path(package, name)
        except ValueError:
            errors.append("PATH: invalid inventory path")
            continue
        if name not in actual:
            continue
        if path.is_symlink():
            errors.append("PATH: symlink not permitted: " + name)
            continue
        if digest(path) != checksum:
            errors.append("HASH: " + name)
        try:
            text = path.read_text(encoding="utf-8-sig")
        except UnicodeError:
            errors.append("ENCODING: " + name)
            continue
        if path.suffix in {".json", ".jsonl"}:
            lines = text.splitlines() if path.suffix == ".jsonl" else [text]
            for number, line in enumerate(lines, 1):
                try:
                    parse_json(line)
                except (ValueError, TypeError):
                    errors.append(f"JSON: {name}: record {number}")
        for rule, pattern in SECRETS.items():
            for match in re.finditer(pattern, text):
                line = text[:match.start()].count("\n") + 1
                errors.append(f"SECRET: {name}:{line}: {rule} (value redacted)")
        if path.suffix == ".md":
            for value in re.findall(r"\]\(([^)]+)\)", text):
                value = value.strip("<>").replace("\\", "/")
                if urlsplit(value).scheme:
                    continue
                target = (path.parent / unquote(value.split("#")[0])).resolve()
                if not target.is_relative_to(boundary) or not target.exists():
                    errors.append("REFERENCE: " + name + " (missing or escaping link)")
    return errors


def category(name):
    if name.startswith(("runtime/", "harness/", "sql/")):
        return "code_harness"
    if name.startswith(("evidence/runs/", "manifests/history/")):
        return "evidence_history"
    return "documentation_specification"


def require_hash(path, expected):
    if not re.fullmatch(r"[a-fA-F0-9]{64}", expected) or digest(path) != expected.lower():
        raise ValueError("embedded hash mismatch: " + path.name)


def require_file(root, value):
    path = safe_path(root, value)
    if not path.is_file():
        raise ValueError("missing referenced file")
    return path


def validate_metadata(root, package):
    """Verify current pointers without rewriting or re-judging historical runs."""
    errors = []
    try:
        pointer = load_json(package / "manifests/security.latest-observation.json")
        candidate = require_file(package / "manifests", pointer["candidate"])
        require_hash(candidate, pointer["candidateSha256"])
        for key in ("observed", "comparison", "graph"):
            require_file(package, pointer[key])
        require_file(package, pointer["humanAcceptance"]["record"])
        expected = load_json(candidate)
        for name, checksum in expected["allowlistScope"]["approvedSources"].items():
            require_hash(require_file(root, name), checksum)
        grants = sum(len(o["effectiveGrants"]) for o in expected["owners"])
        if grants != 178:
            raise ValueError("current approved candidate capability count differs")
        run = package / "evidence/runs" / pointer["run"]
        baseline = require_file(package, "evidence/runs/" + pointer["baselineRun"] + "/results.json")
        require_file(run, "results.json")
        comparison = load_json(require_file(package, pointer["comparison"]))
        require_hash(run / "preflight.expected.json", comparison["expectedSha256"])
        observed = load_json(require_file(package, pointer["observed"]))
        require_hash(run / "catalog.jsonl", observed["catalogSha256"])
        if load_json(run / "preflight.expected.json") != expected:
            raise ValueError("current expectation differs from frozen preflight")
        for name, checksum in load_json(run / "source-hashes.json").items():
            require_hash(require_file(package / "runtime", name), checksum)
        scenarios = load_json(package / "proofs/scenarios.json")
        ids = [case["id"] for case in scenarios["cases"]]
        if len(ids) != 54 or len(set(ids)) != 54 or scenarios["count"] != 54:
            raise ValueError("54 scenario identifiers not preserved")
        for path in (baseline, run / "results.json"):
            if set(load_json(path)["cases"]) != set(ids):
                raise ValueError("run scenario references differ")
        # The historical plan/source hashes describe those revisions, not today's
        # mutable plan. The originals lock preserves them; do not compare them
        # against changed current sources or require every old run to PASS.
    except (ValueError, KeyError, TypeError, OSError) as exc:
        errors.append("METADATA: " + str(exc))
    return errors


def validate(root):
    package = root / PACKAGE
    lock = load_json(LOCK)
    expected = lock["files"]
    counts = {key: sum(category(n) == key for n in expected)
              for key in ("code_harness", "evidence_history", "documentation_specification")}
    if len(expected) != 272 or counts != {"code_harness": 12, "evidence_history": 248,
                                         "documentation_specification": 12}:
        return ["INVENTORY: invalid original file lock"]
    result = subprocess.run(["git", "ls-files", "-z", "--cached", "--others",
                             "--exclude-standard", "--", PACKAGE + "/"], cwd=root,
                            capture_output=True, check=True)
    names = result.stdout.decode("utf-8").split("\0")
    candidates = [safe_path(root, name) for name in names if name]
    errors = validate_files(package, expected, candidates, root)
    # Prevent Git from changing historical CRLF evidence on Linux/Windows checkout.
    attrs = subprocess.run(["git", "check-attr", "-z", "--stdin", "text"], cwd=root,
                           input=b"".join(name.encode() + b"\0" for name in names if name),
                           capture_output=True, check=True).stdout.decode().split("\0")
    if any(attrs[i] != "unset" for i in range(2, len(attrs) - 1, 3)):
        errors.append("ATTRIBUTES: original files require -text to preserve exact bytes")
    errors.extend(validate_metadata(root, package))
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    try:
        errors = validate(args.root.resolve())
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError):
        # Malformed input must fail, without printing credentials from its content.
        errors = ["VALIDATION: invalid input or unavailable Git metadata"]
    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        print(f"FAIL: {len(errors)} validation violation(s)", file=sys.stderr)
        return 1
    print("PASS: 272 originals (12/248/12), JSON/JSONL, references, hashes, secrets and Git attributes; no database executed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
