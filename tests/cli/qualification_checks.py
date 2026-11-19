"""Language-tolerant assertions shared by the native CLI qualification recipe."""
import re
import json
import shutil
from pathlib import Path


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


def copy_localization_catalogs(source, destination):
    catalogs = Path(source) / 'Localization'
    if not catalogs.is_dir():
        return
    target = Path(destination) / 'Localization'
    target.mkdir(parents=True, exist_ok=True)
    for file in catalogs.glob('*.json'):
        shutil.copy2(file, target / file.name)


def resolve_packed_module(output, module_id):
    matches = [manifest.parent for manifest in Path(output).rglob('module.json')
               if json.loads(manifest.read_text(encoding='utf-8')).get('id') == module_id]
    if len(matches) != 1:
        raise ValueError(f"Expected one packed module '{module_id}', found {len(matches)} under {output}")
    return matches[0].resolve()
