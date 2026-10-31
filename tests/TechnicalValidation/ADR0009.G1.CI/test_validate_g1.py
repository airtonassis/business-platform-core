"""Offline validator tests. All mutations are confined to TemporaryDirectory."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import shutil
import subprocess
import sys

import validate_g1 as validator


class FileValidationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "data.json").write_text('{"ok": true}\n', encoding="utf-8")
        (self.root / "rows.jsonl").write_text('{"id": 1}\n{"id": 2}\n', encoding="utf-8")
        (self.root / "README.md").write_text('[data](data.json)\n', encoding="utf-8")
        self.expected = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                         for p in self.root.iterdir()}

    def check(self):
        return validator.validate_files(self.root, self.expected)

    def assertViolation(self, code):
        self.assertTrue(any(e.startswith(code + ":") for e in self.check()), code)

    def test_valid_files_are_read_only_and_accepted(self):
        self.assertEqual([], self.check())
        self.assertEqual(self.expected, {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                                         for p in self.root.iterdir()})

    def test_missing_expected_file_fails(self):
        (self.root / "data.json").unlink()
        self.assertViolation("MISSING")

    def test_unexpected_file_fails(self):
        (self.root / "extra.txt").write_text("unexpected", encoding="utf-8")
        self.assertViolation("UNEXPECTED")

    def test_modified_evidence_fails_even_when_json_valid(self):
        (self.root / "data.json").write_text('{"ok": false}', encoding="utf-8")
        self.assertViolation("HASH")

    def test_malformed_json_fails_even_if_hash_matches(self):
        p = self.root / "data.json"
        p.write_text('{"invalid":', encoding="utf-8")
        self.expected[p.name] = hashlib.sha256(p.read_bytes()).hexdigest()
        self.assertViolation("JSON")

    def test_jsonl_checks_every_line(self):
        (self.root / "rows.jsonl").write_text('{"id": 1}\nnot-json\n', encoding="utf-8")
        self.assertViolation("JSON")

    def test_duplicate_keys_are_rejected(self):
        (self.root / "data.json").write_text('{"id":1,"id":2}', encoding="utf-8")
        self.assertViolation("JSON")

    def test_non_finite_json_is_rejected(self):
        (self.root / "data.json").write_text('{"value":NaN}', encoding="utf-8")
        self.assertViolation("JSON")

    def test_secret_diagnostic_redacts_value(self):
        secret = "ghp_" + "A" * 36
        (self.root / "data.json").write_text(json.dumps({"value": secret}), encoding="utf-8")
        errors = self.check()
        self.assertTrue(any(e.startswith("SECRET:") for e in errors))
        self.assertNotIn(secret, "\n".join(errors))

    def test_broken_markdown_link_fails(self):
        (self.root / "README.md").write_text('[missing](missing.json)', encoding="utf-8")
        self.assertViolation("REFERENCE")

    def test_windows_link_is_normalized_only_during_read(self):
        p = self.root / "README.md"
        p.write_text('[data](.\\data.json)', encoding="utf-8")
        self.expected[p.name] = hashlib.sha256(p.read_bytes()).hexdigest()
        before = p.read_bytes()
        self.assertEqual([], self.check())
        self.assertEqual(before, p.read_bytes())

    def test_generated_candidate_is_rejected_even_if_approved_by_input_lock(self):
        p = self.root / "payload.dll"
        p.write_bytes(b"generated")
        self.expected[p.name] = hashlib.sha256(p.read_bytes()).hexdigest()
        self.assertViolation("GENERATED")

    def test_build_outputs_not_candidates_are_ignored(self):
        p = self.root / "runtime/bin/generated.dll"
        p.parent.mkdir(parents=True)
        p.write_bytes(b"generated")
        self.assertEqual([], self.check())

    def test_explicit_build_candidate_is_rejected(self):
        p = self.root / "runtime/obj/generated.json"
        p.parent.mkdir(parents=True)
        p.write_text("{}", encoding="utf-8")
        errors = validator.validate_files(self.root, self.expected, candidates=[p])
        self.assertTrue(any(e.startswith("GENERATED:") for e in errors))


class SafePathTests(unittest.TestCase):
    def test_windows_paths_resolve_inside_root(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.assertEqual(root / "a/b.json", validator.safe_path(root, "a\\b.json"))

    def test_absolute_and_traversing_manifest_paths_are_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            for value in ["../escape", "C:\\secret", "/etc/passwd", "a/../../escape"]:
                with self.subTest(value=value), self.assertRaises(ValueError):
                    validator.safe_path(Path(temp), value)


class GitWhitespaceTests(unittest.TestCase):
    def test_preserved_crlf_is_a_line_ending_not_trailing_whitespace(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = Path(__file__).resolve().parents[3]
            shutil.copyfile(source / ".gitattributes", root / ".gitattributes")
            path = root / validator.PACKAGE / "approved.json"
            path.parent.mkdir(parents=True)
            path.write_bytes(b'{"historical": true}\r\n')
            subprocess.run(["git", "init", "--quiet", str(root)], check=True,
                           capture_output=True)
            subprocess.run(["git", "add", "--", path.relative_to(root).as_posix()],
                           cwd=root, check=True, capture_output=True)
            result = subprocess.run(["git", "diff", "--cached", "--check"],
                                    cwd=root, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertEqual(b'{"historical": true}\r\n', path.read_bytes())


class IntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory()
        cls.root = Path(cls.temp.name)
        cls.source = Path(__file__).resolve().parents[3]
        cls.package = cls.root / validator.PACKAGE
        shutil.copytree(cls.source / validator.PACKAGE, cls.package,
                        ignore=shutil.ignore_patterns("bin", "obj"))
        # Copy only files referenced by package Markdown or current source hashes.
        for source in (cls.source / "docs").rglob("*.md"):
            target = cls.root / source.relative_to(cls.source)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
        shutil.copyfile(cls.source / ".gitattributes", cls.root / ".gitattributes")
        subprocess.run(["git", "init", "--quiet", str(cls.root)], check=True,
                       capture_output=True)

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def mutate(self, relative, content):
        path = self.root / relative
        original = path.read_bytes()
        self.addCleanup(path.write_bytes, original)
        path.write_bytes(content)

    def invoke(self):
        return subprocess.run([sys.executable, "-B", str(Path(validator.__file__)),
                               "--root", str(self.root)], capture_output=True, text=True)

    def test_real_package_and_windows_manifest_paths_pass_without_writes(self):
        result = self.invoke()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("PASS: 272 originals", result.stdout)
        lock = json.loads(validator.LOCK.read_text(encoding="utf-8"))["files"]
        self.assertTrue(all(hashlib.sha256((self.package / name).read_bytes()).hexdigest() == h
                            for name, h in lock.items()))

    def test_cli_returns_failure_for_tampered_evidence(self):
        self.mutate(validator.PACKAGE + "/evidence/runs/ece79aa930df/catalog.jsonl", b"{}\n")
        result = self.invoke()
        self.assertEqual(1, result.returncode)
        self.assertIn("HASH:", result.stderr)

    def test_metadata_hash_detects_changed_frozen_document(self):
        self.mutate("docs/architecture/decisions/ADR-0008-organization-membership-and-context.md",
                    b"modified frozen document\n")
        errors = validator.validate_metadata(self.root, self.package)
        self.assertTrue(any("embedded hash mismatch" in e for e in errors))

    def test_metadata_rejects_missing_current_evidence(self):
        name = validator.PACKAGE + "/manifests/security.latest-observation.json"
        pointer = json.loads((self.root / name).read_text())
        pointer["graph"] = "evidence/runs/ece79aa930df/missing.json"
        self.mutate(name, json.dumps(pointer).encode())
        errors = validator.validate_metadata(self.root, self.package)
        self.assertTrue(any("missing referenced file" in e for e in errors))

    def test_byte_normalization_protection_is_required(self):
        self.mutate(".gitattributes", b"* text=auto\n")
        result = self.invoke()
        self.assertEqual(1, result.returncode)
        self.assertIn("ATTRIBUTES:", result.stderr)


if __name__ == "__main__":
    unittest.main()
