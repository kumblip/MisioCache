#!/usr/bin/env python3
"""Package a published Jellyfin plugin without runtime framework DLLs."""
import hashlib
import json
import os
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile
ROOT=Path(__file__).resolve().parents[1]
DLL='Jellyfin.Plugin.MisioCache.dll'

def package():
    meta=json.loads((ROOT/'meta.json').read_text(encoding='utf-8'))
    version=meta['version']
    if not re.fullmatch(r'\d+\.\d+\.\d+\.\d+',version): raise ValueError('Wersja X.Y.Z.W wymagana')
    if meta['targetAbi']!='12.4.0.0':raise ValueError('Wersja ABI niezgodna z Jellyfin 12.4')
    if meta.get('assemblies')!=[DLL]:raise ValueError('Nieprawidlowa lista assemblies')
    project=ROOT/'src/Jellyfin.Plugin.MisioCache/Jellyfin.Plugin.MisioCache.csproj'
    pver=ET.parse(project).findtext('.//AssemblyVersion')
    if version!=pver:raise ValueError('Nie zsynchronizowano csproj z meta.json')
    compiled=ROOT/'build/publish'/DLL
    if not compiled.is_file() or compiled.stat().st_size<1000:raise FileNotFoundError('Nie ma skompilowanej wtyczki: '+str(compiled))
    dest=ROOT/'dist';dest.mkdir(parents=True,exist_ok=True)
    archive=dest/f'MisioCache_{version}_JF12.4.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        z.write(compiled,DLL)
        z.write(ROOT/'meta.json','meta.json')
    with zipfile.ZipFile(archive) as z:
        if sorted(z.namelist())!=sorted([DLL,'meta.json']):raise ValueError('Niepoprawna struktura ZIP')
        if z.testzip() is not None:raise ValueError('Uszkodzony ZIP')
    data=archive.read_bytes()
    for ext,digest in [('.md5',hashlib.md5(data).hexdigest()),('.sha256',hashlib.sha256(data).hexdigest())]:
        (dest/(archive.name+ext)).write_text(digest+'\n',encoding='ascii')
    print('Release ZIP:',archive)
    if os.getenv('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'],'a',encoding='utf-8') as f:
            f.write(f'version={version}\ntag=v{version}\narchive={archive.relative_to(ROOT).as_posix()}\narchive_name={archive.name}\n')
    return archive
if __name__=='__main__':package()
