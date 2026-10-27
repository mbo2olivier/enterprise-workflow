#!/usr/bin/env python3
"""Prepare an isolated sample for the final L9b recipe; never copy the developer database."""
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

repo = Path(__file__).resolve().parent.parent
configure_only = len(sys.argv) == 3 and sys.argv[1] == "--configure-nuget"
if len(sys.argv) != 2 and not configure_only:
    raise SystemExit("Usage: python3 scripts/prepare-l9b-manual.py [--configure-nuget] <output-directory>")
output = Path(sys.argv[-1]).expanduser().resolve()

def configure_nuget():
    feed = output / "feed"
    feed.mkdir(exist_ok=True)
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="local", value=str(feed))
    ET.SubElement(sources, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json")
    mapping = ET.SubElement(config, "packageSourceMapping")
    local = ET.SubElement(mapping, "packageSource", key="local")
    ET.SubElement(local, "package", pattern="EnterpriseWorkflow.*")
    public = ET.SubElement(mapping, "packageSource", key="nuget.org")
    ET.SubElement(public, "package", pattern="*")
    settings = ET.SubElement(config, "config")
    ET.SubElement(settings, "add", key="globalPackagesFolder", value=str(output / "packages"))
    ET.indent(config)
    ET.ElementTree(config).write(output / "NuGet.Config", encoding="utf-8", xml_declaration=True)

if configure_only:
    if not (output / "LeaveRequest.Module").is_dir():
        raise SystemExit("Expected an existing recipe directory containing LeaveRequest.Module.")
    configure_nuget()
    print(f"Configured local NuGet source: {output / 'feed'}")
    raise SystemExit(0)
if output.exists():
    raise SystemExit("Choose a new directory; existing files will not be overwritten.")
module = output / "LeaveRequest.Module"
module.mkdir(parents=True)
source = repo / "samples" / "LeaveRequest.Module"
for item in source.iterdir():
    if item.is_file() and (item.suffix in {".cs", ".razor", ".csproj"} or item.name == "ew.dev.json"):
        shutil.copy2(item, module / item.name)
for name in ("Directory.Build.props", "Directory.Packages.props", "global.json"):
    shutil.copy2(repo / name, output / name)
project = next(module.glob("*.csproj"))
tree = ET.parse(project)
for reference in tree.findall(".//ProjectReference"):
    reference.set("Include", str((source / reference.get("Include")).resolve()))
tree.write(project, encoding="unicode")
configure_nuget()
print(f"Prepared: {module}")
print(f"Build local packages before starting: ./scripts/pack-local.sh {output / 'feed'}")
print("The development database and historical artifacts were not copied.")
