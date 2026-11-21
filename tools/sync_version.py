#!/usr/bin/env python3
"""Validate one source of truth (meta.json), and synchronize .csproj on CI."""
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def sync_version():
    meta=json.loads((ROOT/'meta.json').read_text(encoding='utf-8'))
    version=meta['version']
    if not re.fullmatch(r'\d+\.\d+\.\d+\.\d+',version):
        raise SystemExit('meta.json: wymagany numer wersji X.Y.Z.W')
    if meta['targetAbi']!='12.4.0.0':
        raise SystemExit('targetAbi musi odpowiadac Jellyfin 12.4.0.0')
    project=ROOT/'src/Jellyfin.Plugin.MisioCache/Jellyfin.Plugin.MisioCache.csproj'
    source=project.read_text(encoding='utf-8')
    for tag,value in [('Version',version.removesuffix('.0')),('AssemblyVersion',version),('FileVersion',version)]:
        source,n=re.subn(r'(<'+tag+r'>)[^<]*(</'+tag+r'>)',lambda m:m.group(1)+value+m.group(2),source,count=1)
        if n!=1:raise SystemExit('csproj: brak znacznika '+tag)
    ET.fromstring(source)
    project.write_text(source,encoding='utf-8')
    print('MisioCache build:',version)
    return version

if __name__=='__main__':sync_version()
