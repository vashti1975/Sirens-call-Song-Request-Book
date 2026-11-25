"""Prepare both build-verified companions and a shared Dalamud repository feed."""
import argparse
import hashlib
import json
import re
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path
from importlib.util import spec_from_file_location, module_from_spec

ROOT = Path(__file__).resolve().parents[1]
spec = spec_from_file_location('package_plugin', ROOT / 'scripts/package-plugin.py')
packager = module_from_spec(spec)
spec.loader.exec_module(packager)

def prepare_release(repository):
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository):
        raise ValueError('Expected a GitHub owner/repository.')
    output = ROOT / 'plugin-dist'
    output.mkdir(exist_ok=True)
    project = ET.parse(ROOT / 'bridge/SongbookSync.csproj').getroot()
    version = project.findtext('.//Version')
    if not re.fullmatch(r'\d+\.\d+\.\d+', version or ''):
        raise ValueError('A stable three-part release version is required.')
    base = f'https://github.com/{repository}/releases/download/v{version}'
    entries, checksums = [], []
    for internal, folder in [('SongbookSync', 'bridge'), ('TramaticSync', 'bridge-tramatic')]:
        packages = list((ROOT / folder / 'bin').rglob(f'{internal}/latest.zip'))
        if len(packages) != 1:
            raise ValueError(f'Expected exactly one compiled SDK package for {internal}.')
        temporary = output / internal
        manifest = packager.prepare(packages[0], temporary, base, internal_name=internal, archive_name=internal+'.zip')
        if manifest['AssemblyVersion'] != version+'.0':
            raise ValueError('Both companions must use the same release version.')
        archive = output / (internal+'.zip')
        shutil.copyfile(temporary / archive.name, archive)
        checksums.append(hashlib.sha256(archive.read_bytes()).hexdigest()+'  '+archive.name)
        entries.extend(json.loads((temporary / 'pluginmaster.json').read_text(encoding='utf-8')))
    (output / 'pluginmaster.json').write_text(json.dumps(entries, indent=2)+'\n', encoding='utf-8')
    (output / 'SHA256SUMS.txt').write_text('\n'.join(checksums)+'\n', encoding='utf-8')
    feed = f'https://raw.githubusercontent.com/{repository}/main/pluginmaster.json'
    notes = f'''Siren's call Song Request Book {version}: MidiBard 2 Alpha playback compatibility.

Adds support for Zune's MidiBard 2 Alpha instance playback layout (ZuneBardAlpha 1.15.0.171), while preserving standard MidiBard 2 support. Detects the active command owner on each poll so disabled or reloaded copies cannot mask playback. The status command now shows the detected playback source and version. Existing pairing keys and filename matches remain valid.

Add this URL in /xlsettings > Experimental > Custom Plugin Repositories:
{feed}

Then open /xlplugins and install ONE companion on the lead bard:
- Siren's call Song Request Book (SongbookSync): configurable hosted songbook; /songbooksync HTTPS_URL TOKEN.
- Siren's call Song Request Book (TramaticSync): compatible with the existing Tramatic installation and saved key; /tramaticsync status.

Existing installations keep their saved pairing key and settings. Enable your chosen MidiBard plugin, update one songbook companion on the lead bard, then start a new song. Check /tramaticsync status (or /songbooksync status) for the detected playback source. No new connection key is needed for this update.

Built against Dalamud API 15 with .NET 10. Tests cover Alpha instance discovery, standard static playback, file matching, song starts and changes, pause/resume, plugin reloads, announcement identity and installer packaging. The Alpha package's playback members were inspected against ZuneBardAlpha 1.15.0.171. Live FFXIV playback and chat delivery still require an in-game check.

Packages contain only the compiled companion, its matching manifest and AGPL-3.0 license; no songs, private artwork, connection keys, MidiBard DLLs or game libraries.
'''
    (output / 'release-notes.md').write_text(notes, encoding='utf-8')
    print(f'Prepared v{version}: two compiled companions, checksums and a shared installer feed.')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repository', required=True)
    prepare_release(parser.parse_args().repository)
