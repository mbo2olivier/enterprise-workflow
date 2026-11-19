import unittest
import json
from pathlib import Path
from tempfile import TemporaryDirectory
from qualification_checks import compilation_failure_retained_runtime, identity_selector_available, copy_localization_catalogs, resolve_packed_module


class QualificationChecksTests(unittest.TestCase):
    def test_rebuild_failure_accepts_french_and_english(self):
        for message in ("Ancienne génération toujours active.", "Previous generation is still active."):
            with self.subTest(message=message):
                self.assertTrue(compilation_failure_retained_runtime("error CS1519\n" + message))

    def test_compiler_error_or_stopped_runtime_does_not_prove_retention(self):
        for message in ("error CS1519", "Runtime stopped.", "Runtime arrêté.", ""):
            with self.subTest(message=message):
                self.assertFalse(compilation_failure_retained_runtime(message))

    def test_selector_is_detected_in_both_languages(self):
        for title in ("Développement", "Development"):
            html = f'<h1>{title}</h1><form method="post" action="/__ew/dev/identity"><select name="identity"></select></form>'
            with self.subTest(title=title):
                self.assertTrue(identity_selector_available(html))

    def test_translated_title_alone_is_not_a_ready_selector(self):
        self.assertFalse(identity_selector_available("<h1>Développement</h1>"))
        self.assertFalse(identity_selector_available('<form action="/auth/login"><select name="identity"></select></form>'))

    def test_selector_accepts_attribute_order_and_single_quotes(self):
        self.assertTrue(identity_selector_available("<form class='form' action = '/__ew/dev/identity'><select class='input' name = 'identity'></select></form>"))


class ArtifactPreparationTests(unittest.TestCase):
    def test_artifact_is_selected_from_manifest_without_a_fixed_version(self):
        with TemporaryDirectory() as root:
            artifact = Path(root) / 'sample.leave-request' / '1.9.0'
            artifact.mkdir(parents=True)
            (artifact / 'module.json').write_text(json.dumps({'id': 'sample.leave-request', 'version': '1.9.0'}))
            self.assertEqual(artifact.resolve(), resolve_packed_module(root, 'sample.leave-request'))

    def test_missing_artifact_is_an_explicit_error(self):
        with TemporaryDirectory() as root:
            with self.assertRaisesRegex(ValueError, 'found 0'):
                resolve_packed_module(root, 'sample.leave-request')

    def test_multiple_versions_are_not_selected_arbitrarily(self):
        with TemporaryDirectory() as root:
            for version in ('1.8.0', '1.9.0'):
                folder = Path(root) / version
                folder.mkdir()
                (folder / 'module.json').write_text(json.dumps({'id': 'sample.leave-request', 'version': version}))
            with self.assertRaisesRegex(ValueError, 'found 2'):
                resolve_packed_module(root, 'sample.leave-request')

    def test_catalogs_are_copied_with_their_paths_without_copying_local_secrets(self):
        with TemporaryDirectory() as root:
            source, target = Path(root) / 'source', Path(root) / 'target'
            catalogs = source / 'Localization'
            catalogs.mkdir(parents=True)
            for name in ('en.json', 'fr.json'):
                (catalogs / name).write_text('{"title":"Example"}')
            (source / '.env').write_text('SECRET=not-for-copy')
            (catalogs / '.env').write_text('SECRET=not-for-copy')
            copy_localization_catalogs(source, target)
            self.assertEqual(['en.json', 'fr.json'], sorted(p.name for p in (target / 'Localization').iterdir()))
            self.assertFalse((target / '.env').exists())

    def test_modules_without_catalogs_remain_supported(self):
        with TemporaryDirectory() as root:
            target = Path(root) / 'target'
            copy_localization_catalogs(Path(root) / 'source', target)
            self.assertFalse(target.exists())


if __name__ == '__main__':
    unittest.main()
