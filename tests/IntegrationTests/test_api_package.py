import importlib.util
import json
import tarfile
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('package_api', ROOT / 'scripts/package_api.py')
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


class ApiPackageTests(unittest.TestCase):
    def test_only_runtime_files_are_packaged_and_outputs_are_not_overwritten(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            publish = root / 'publish'
            publish.mkdir()
            (publish / 'Business.Platform.Core.Api.deps.json').write_text(json.dumps({
                'targets': {'net10.0': {'api': {'runtime': {'Api.dll': {}}}}}}))
            (publish / 'Business.Platform.Core.Api.runtimeconfig.json').write_text('{}')
            (publish / 'Api.dll').write_bytes(b'synthetic-assembly')
            for name in ('.env', 'appsettings.Production.json', 'Api.pdb'):
                (publish / name).write_text('never-package')
            output = root / 'context.tar.gz'
            package.create(publish, output)
            with tarfile.open(output) as archive:
                names = archive.getnames()
                self.assertIn('publish/Api.dll', names)
                self.assertNotIn('publish/.env', names)
                self.assertNotIn('publish/appsettings.Production.json', names)
                self.assertNotIn('publish/Api.pdb', names)
            with self.assertRaises(FileExistsError):
                package.create(publish, output)

    def test_unknown_non_assembly_runtime_asset_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'Business.Platform.Core.Api.deps.json').write_text(json.dumps({
                'targets': {'net10.0': {'bad': {'runtime': {'../secret': {}}}}}}))
            with self.assertRaisesRegex(ValueError, 'UNSUPPORTED_RUNTIME_ASSET'):
                package.create(root, root / 'context.tar.gz')


if __name__ == '__main__':
    unittest.main()
