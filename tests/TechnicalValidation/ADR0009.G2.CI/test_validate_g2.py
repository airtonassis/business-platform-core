import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("g2", Path(__file__).with_name("validate_g2.py"))
g2 = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(g2)
REPO = Path(__file__).resolve().parents[3]


class Checks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def fixture(self):
        names = [g2.G2 + "/coverage.json", g2.G2 + "/RASTREABILIDADE_COMPLEMENTAR.json"]
        for run in g2.RUNS:
            names += [g2.G2 + f"/evidence/runs/{run}/{f}" for f in
                      ("results.json", "selection.json", "assertions.json")]
        names += [g2.G1 + "/proofs/scenarios.json", "docs/architecture/PLANO_VALIDACAO_TECNICA_ADR_0009.md"]
        for name in names:
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(REPO / name, target)

    def mutate(self, name, change):
        path = self.root / name
        value = json.loads(path.read_text(encoding="utf-8-sig"))
        change(value)
        path.write_text(json.dumps(value), encoding="utf-8")

    def test_valid_coverage(self):
        self.fixture()
        g2.validate_coverage(self.root)
        g2.validate_traceability(self.root)

    def test_hash_divergence(self):
        p = self.root / "a.json"
        p.write_text("{}")
        with self.assertRaises(ValueError):
            g2.check_hashes(self.root, {"a.json": "0" * 64})

    def test_missing_file(self):
        with self.assertRaises(ValueError):
            g2.check_hashes(self.root, {"absent": "0" * 64})

    def test_json_invalid(self):
        p = self.root / "a.json"
        p.write_text("{bad}")
        with self.assertRaises(ValueError):
            g2.inspect_text(self.root, p)

    def test_jsonl_invalid(self):
        p = self.root / "a.jsonl"
        p.write_text('{}\n{"bad":\n')
        with self.assertRaises(ValueError):
            g2.inspect_text(self.root, p)

    def test_duplicate_json_key(self):
        p = self.root / "a.json"
        p.write_text('{"x":1,"x":2}')
        with self.assertRaises(ValueError):
            g2.inspect_text(self.root, p)

    def test_missing_markdown_reference(self):
        p = self.root / "a.md"
        p.write_text("[missing](absent.json)")
        with self.assertRaises(ValueError):
            g2.inspect_text(self.root, p)

    def test_windows_manifest_path(self):
        (self.root / "dir").mkdir()
        p = self.root / "dir/a.json"
        p.write_text("{}")
        g2.check_hashes(self.root, {"dir\\a.json": g2.digest(p)})

    def test_traversal_rejected(self):
        with self.assertRaises(ValueError):
            g2.check_hashes(self.root, {"../outside": "0" * 64})

    def test_duplicate_variant(self):
        self.fixture()
        self.mutate(g2.G2 + "/coverage.json", lambda d: d["variants"].__setitem__(1, d["variants"][0]))
        with self.assertRaises(ValueError):
            g2.validate_coverage(self.root)

    def test_incompatible_result(self):
        self.fixture()
        self.mutate(g2.G2 + f"/evidence/runs/{g2.RUNS[0]}/results.json",
                    lambda d: d["subproofs"][0].__setitem__("status", "FAIL"))
        with self.assertRaises(ValueError):
            g2.validate_coverage(self.root)

    def test_unexpected_gate(self):
        self.fixture()
        self.mutate(g2.G2 + "/coverage.json", lambda d: d["variants"][0].__setitem__("id", "K01"))
        with self.assertRaises(ValueError):
            g2.validate_coverage(self.root)

    def test_invalid_trace_pointer(self):
        self.fixture()
        self.mutate(g2.G2 + "/RASTREABILIDADE_COMPLEMENTAR.json",
                    lambda d: d["variants"][0]["resultReference"].__setitem__("jsonPointer", "/subproofs/9999"))
        with self.assertRaises(ValueError):
            g2.validate_traceability(self.root)

    def test_trace_value_mismatch(self):
        self.fixture()
        self.mutate(g2.G2 + "/RASTREABILIDADE_COMPLEMENTAR.json",
                    lambda d: d["variants"][0]["requirements"][0].__setitem__("protectionAndTraceability", "invented"))
        with self.assertRaises(ValueError):
            g2.validate_traceability(self.root)

    def test_secret_redacted(self):
        p = self.root / "secret.txt"
        token = "ghp_" + "a" * 36
        p.write_text(token)
        with self.assertRaises(ValueError) as error:
            g2.inspect_text(self.root, p)
        self.assertNotIn(token, str(error.exception))

    def test_nonhistorical_log_forbidden(self):
        with self.assertRaises(ValueError):
            g2.check_candidate("debug.log")

    def test_build_file_forbidden(self):
        for name in ("runtime/obj/file.json", "__pycache__/x.pyc", "runtime/bin/a.dll"):
            with self.subTest(name=name), self.assertRaises(ValueError):
                g2.check_candidate(name)

    def test_validator_source_scans_cleanly(self):
        g2.inspect_text(REPO, Path(__file__).with_name("validate_g2.py"))

    def test_workflow_write_permission_rejected(self):
        p = self.root / g2.WORKFLOW
        p.parent.mkdir(parents=True)
        p.write_text((REPO / g2.WORKFLOW).read_text().replace("contents: read", "contents: write"))
        with self.assertRaises(ValueError):
            g2.validate_workflow(self.root)

    def test_workflow_extra_command_rejected(self):
        p = self.root / g2.WORKFLOW
        p.parent.mkdir(parents=True)
        p.write_text((REPO / g2.WORKFLOW).read_text() + "\n      - run: echo unauthorized\n")
        with self.assertRaises(ValueError):
            g2.validate_workflow(self.root)


if __name__ == "__main__":
    unittest.main()
