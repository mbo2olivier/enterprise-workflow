import unittest
from qualification_checks import compilation_failure_retained_runtime, identity_selector_available


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


if __name__ == '__main__':
    unittest.main()
