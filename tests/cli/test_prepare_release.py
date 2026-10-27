"""Release inventory must reject incomplete or unsafe artifacts before publication."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('prepare_release', Path(__file__).resolve().parents[2] / 'scripts/prepare-release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class ReleaseTests(unittest.TestCase):
    def build(self, extra=None, dependency='', version='1.0.0'):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'release').mkdir()
            (root / 'release/catalog.json').write_text(json.dumps({
                'version': '1.0.0', 'authors': 'Contributors', 'licenseExpression': 'Apache-2.0',
                'repositoryUrl': 'https://example.org', 'projects': ['src/Test.csproj']}))
            def pack(*args, **kwargs):
                with zipfile.ZipFile(root / 'output/Test.1.0.0.nupkg', 'w') as archive:
                    archive.writestr('Test.nuspec', f'<package><metadata><id>Test</id><version>{version}</version><authors>Contributors</authors><license>Apache-2.0</license>{dependency}</metadata></package>')
                    for name in ['README.md', 'LICENSE'] + ([extra] if extra else []):
                        archive.writestr(name, 'fixture')
            with patch.object(release, 'ROOT', root), patch.object(release.subprocess, 'run', side_effect=pack), patch.object(release.subprocess, 'check_output', return_value='fixture'):
                release.prepare(root / 'output', True)
            return json.loads((root / 'output/manifest.json').read_text())

    def test_exact_inventory_and_hash(self):
        manifest = self.build()
        self.assertTrue(manifest['publicMetadataChecked'])
        self.assertEqual(len(manifest['packages']), 1)
        self.assertEqual(len(manifest['packages'][0]['sha256']), 64)

    def test_local_database_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Forbidden package content'):
            self.build(extra='content/data.sqlite')

    def test_wrong_version_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Unexpected identity/version'):
            self.build(version='2.0.0')

    def test_internal_dependency_outside_catalog_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'outside catalog'):
            self.build(dependency='<dependencies><dependency id="EnterpriseWorkflow.Missing" version="1.0.0" /></dependencies>')


if __name__ == '__main__':
    unittest.main()
