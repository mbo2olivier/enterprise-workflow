"""Language-tolerant assertions shared by the native CLI qualification recipe."""
import re


def compilation_failure_retained_runtime(log):
    return any(message in log for message in (
        "Ancienne génération toujours active",
        "Previous generation is still active",
    ))


def identity_selector_available(html):
    # Assert the public form contract, never its translated heading or button.
    return bool(re.search(r'''<form\b[^>]*\baction\s*=\s*["']/__ew/dev/identity["']''', html)) and bool(
        re.search(r'''<select\b[^>]*\bname\s*=\s*["']identity["']''', html)
    )
