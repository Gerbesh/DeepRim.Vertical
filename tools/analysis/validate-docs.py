"""Offline checks for wiki links, manifests, XML and private output exclusions."""
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def main():
    versions = json.loads((ROOT / 'tools/analysis/versions.json').read_text(encoding='utf-8-sig'))
    package = json.loads((ROOT / 'tools/codegraph/package.json').read_text(encoding='utf-8-sig'))
    assert package['dependencies']['@colbymchenry/codegraph'] == versions['codegraph']
    pages = list((ROOT / 'docs/wiki').glob('*.md'))
    assert len(pages) >= 10, 'Missing wiki pages'
    for page in pages:
        body = page.read_text(encoding='utf-8-sig')
        for match in re.finditer(r'\]\(([^)]+)\)', body):
            target = match.group(1).split('#')[0]
            if target and not re.match(r'\w+://', target):
                assert (page.parent / target).is_file(), f'Broken link in {page.name}: {target}'
    for folder in ('About', '1.6/Defs', 'Languages'):
        for path in (ROOT / folder).rglob('*.xml'):
            ET.parse(path)
    for path in (ROOT / 'tools').rglob('*.json'):
        json.loads(path.read_text(encoding='utf-8-sig'))
    tracked = subprocess.check_output(['git', 'ls-files'], cwd=ROOT, text=True).splitlines()
    forbidden = ('.tools/', '.analysis/', '.codegraph/', '.impact_engine/', 'obj/', 'bin/')
    assert not [path for path in tracked if path.startswith(forbidden) or path.endswith(('.dll', '.pdb'))], 'Private/generated files tracked'
    print(f'OK: {len(pages)} wiki pages, local links, XML, JSON manifests and tracked artifacts')


if __name__ == '__main__':
    main()
