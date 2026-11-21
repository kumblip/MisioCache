#!/usr/bin/env python3
"""Generate Jellyfin-compatible manifest release entries (checksum = MD5)."""
import argparse
from datetime import datetime,timezone
import hashlib
import json
from pathlib import Path
import re
import zipfile
from urllib.parse import quote

def update(archive,meta_file,manifest_file,repo,stamp=None):
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+',repo):raise ValueError('owner/repo')
    meta=json.loads(meta_file.read_text(encoding='utf-8'))
    version=meta['version']
    if not re.fullmatch(r'\d+\.\d+\.\d+\.\d+',version):raise ValueError('Wersja X.Y.Z.W')
    if meta['targetAbi']!='12.4.0.0':raise ValueError('ABI')
    if archive.name!=f'MisioCache_{version}_JF12.4.zip':raise ValueError('Niepoprawna nazwa wydania')
    with zipfile.ZipFile(archive) as z:
        if sorted(z.namelist())!=sorted(['Jellyfin.Plugin.MisioCache.dll','meta.json']) or z.testzip() is not None:
            raise ValueError('Niepoprawny ZIP wtyczki')
        inside=json.loads(z.read('meta.json'))
        if inside['guid']!=meta['guid'] or inside['version']!=version:
            raise ValueError('Niepoprawny GUID lub wersja w ZIP')
    entries=json.loads(manifest_file.read_text(encoding='utf-8'))
    if not isinstance(entries,list):raise ValueError('manifest musi byc tablica')
    entry=next((e for e in entries if e.get('guid','').lower()==meta['guid'].lower()),None)
    if entry is None:
        entry={k:meta[k] for k in ('guid','name','description','overview','category')}
        entry['owner']=repo.split('/')[0]
        entry['versions']=[]
        entries.append(entry)
    checksum=hashlib.md5(archive.read_bytes()).hexdigest()
    url=f'https://github.com/{repo}/releases/download/v{quote(version)}/{quote(archive.name)}'
    if any(v.get('version')==version for v in entry['versions']):
        raise ValueError('Ta wersja juz zostala wpisana do manifestu - release jest niezmienny')
    entry['versions'].append({'version':version,'changelog':f'MisioCache {version}: panel ustawien i diagnostyki; bez modyfikowania odtwarzacza.', 'targetAbi':'12.4.0.0','sourceUrl':url,'checksum':checksum,'timestamp':stamp or datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')})
    entry['versions'].sort(key=lambda v:tuple(map(int,v['version'].split('.'))),reverse=True)
    manifest_file.write_text(json.dumps(entries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return entries

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--archive',type=Path,required=True)
    p.add_argument('--meta',type=Path,default=Path('meta.json'))
    p.add_argument('--manifest',type=Path,default=Path('manifest.json'))
    p.add_argument('--repo',default='kumblip/MisioCache')
    a=p.parse_args()
    out=update(a.archive,a.meta,a.manifest,a.repo)
    print(f'Zaktualizowano manifest: {len(out)} wtyczek')
if __name__=='__main__':main()
