import importlib.util
import io
import json
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('publish_release', Path(__file__).resolve().parents[2] / 'scripts/publish-release.py')
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


def package(name, dependencies=(), signature=None, content='original'):
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, 'w') as archive:
        deps = ''.join(f'<dependency id="{d}" version="1.0.0" />' for d in dependencies)
        archive.writestr('package.nuspec', f'<package><metadata><id>{name}</id><version>1.0.0</version><dependencies>{deps}</dependencies></metadata></package>')
        archive.writestr('lib/content', content)
        if signature:
            archive.writestr('.signature.p7s', signature)
    return stream.getvalue()


class PublishTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        (self.root / 'release').mkdir()
        self.catalog = {'version': '1.0.0', 'projects': []}
        self.manifest = {'publicMetadataChecked': True, 'workingTreeDirty': False,
                         'sourceCommit': 'test-commit', 'packages': []}
        self.addCleanup(patch.stopall)
        patch.object(release, 'ROOT', self.root).start()
        patch.dict(release.os.environ, {'NUGET_API_KEY': 'test-only', 'GITHUB_SHA': 'test-commit'}).start()

    def add(self, name, dependencies=()):
        data = package(name, dependencies)
        file = f'{name}.1.0.0.nupkg'
        (self.root / file).write_bytes(data)
        self.catalog['projects'].append(f'src/{name}.csproj')
        self.manifest['packages'].append({'id': name, 'version': '1.0.0', 'file': file,
                                         'sha256': hashlib.sha256(data).hexdigest()})
        self.save()
        return data

    def save(self):
        (self.root / 'release/catalog.json').write_text(json.dumps(self.catalog))
        (self.root / 'manifest.json').write_text(json.dumps(self.manifest))

    def test_dependency_order_and_tools_last(self):
        self.add('EnterpriseWorkflow.Cli')
        self.add('EnterpriseWorkflow.Core', ['EnterpriseWorkflow.Abstractions'])
        self.add('EnterpriseWorkflow.Abstractions')
        self.assertEqual([r['id'] for r in release.inventory(self.root)],
                         ['EnterpriseWorkflow.Abstractions', 'EnterpriseWorkflow.Core', 'EnterpriseWorkflow.Cli'])

    def test_tampered_artifact_blocks_publication(self):
        self.add('EnterpriseWorkflow.Core')
        (self.root / self.manifest['packages'][0]['file']).write_bytes(b'tampered')
        with self.assertRaisesRegex(ValueError, 'hash mismatch'):
            release.inventory(self.root)

    def test_missing_inventory_entry_is_rejected(self):
        self.add('EnterpriseWorkflow.Core')
        self.manifest['packages'] = []
        self.save()
        with self.assertRaisesRegex(ValueError, 'exact release catalog'):
            release.inventory(self.root)

    def test_existing_different_package_blocks_all_pushes(self):
        self.add('EnterpriseWorkflow.Core')
        with patch.object(release, 'remote', return_value=package('EnterpriseWorkflow.Core', content='changed')), patch.object(release.subprocess, 'run') as push:
            with self.assertRaisesRegex(ValueError, 'Existing package differs'):
                release.publish(self.root)
            push.assert_not_called()

    def test_repository_signature_does_not_force_republication(self):
        self.add('EnterpriseWorkflow.Core')
        with patch.object(release, 'remote', return_value=package('EnterpriseWorkflow.Core', signature='repository-signature')), patch.object(release.subprocess, 'run') as push:
            release.publish(self.root)
            push.assert_not_called()

    def test_dirty_build_is_rejected(self):
        self.add('EnterpriseWorkflow.Core')
        self.manifest['workingTreeDirty'] = True
        self.save()
        with self.assertRaisesRegex(ValueError, 'clean'):
            release.inventory(self.root)


if __name__ == '__main__':
    unittest.main()
