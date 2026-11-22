"""Fast regression checks: successful dotnet pack must actually produce the expected package."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts/qualify-l12.py'
SPEC = importlib.util.spec_from_file_location('qualify_l12', SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class PackageChecks(unittest.TestCase):
    def test_manual_commands_use_the_same_cache_and_workflow_packages_stay_local(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            MODULE.write_qualification_nuget_config(root)
            config = ET.parse(root / 'NuGet.Config').getroot()
            self.assertEqual(str(root / 'packages'), config.find("config/add[@key='globalPackagesFolder']").get('value'))
            self.assertEqual(str(root / 'feed'), config.find("packageSources/add[@key='l12-local']").get('value'))
            local = config.findall("packageSourceMapping/packageSource[@key='l12-local']/package")
            self.assertEqual({'EnterpriseWorkflow.*', 'Example.Workflow.*'}, {item.get('pattern') for item in local})
            self.assertEqual('*', config.find("packageSourceMapping/packageSource[@key='nuget.org']/package").get('pattern'))

    def test_missing_package_reports_packability_before_installation(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaisesRegex(ValueError, 'IsPackable=true'):
                MODULE.require_package(Path(folder), 'Example.Workflow.Cli', '1.0.0-rc.1')

    def test_nuspec_identity_and_version_are_checked(self):
        for package_id, version in [('Other.Cli', '1.0.0-rc.1'), ('Example.Workflow.Cli', '1.0.0')]:
            with self.subTest(package_id=package_id, version=version), tempfile.TemporaryDirectory() as folder:
                package = Path(folder) / 'Example.Workflow.Cli.1.0.0-rc.1.nupkg'
                with zipfile.ZipFile(package, 'w') as archive:
                    archive.writestr('package.nuspec', f'<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>{package_id}</id><version>{version}</version></metadata></package>')
                with self.assertRaisesRegex(ValueError, 'Unexpected package identity'):
                    MODULE.require_package(Path(folder), 'Example.Workflow.Cli', '1.0.0-rc.1')

    def test_matching_package_is_accepted(self):
        with tempfile.TemporaryDirectory() as folder:
            package = Path(folder) / 'Example.Workflow.Cli.1.0.0-rc.1.nupkg'
            with zipfile.ZipFile(package, 'w') as archive:
                archive.writestr('package.nuspec', '<package><metadata><id>Example.Workflow.Cli</id><version>1.0.0-rc.1</version></metadata></package>')
            self.assertEqual(package, MODULE.require_package(Path(folder), 'Example.Workflow.Cli', '1.0.0-rc.1'))


if __name__ == '__main__':
    unittest.main()
