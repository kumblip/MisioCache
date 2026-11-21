#!/usr/bin/env python3
"""Auto-select next four-segment release from main metadata, manifest and git tags.

Initial publication: 0.1.0.0. Next approved publication: 0.1.1.0.
The automated GitHub release workflow executes --write on approved commits to main.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[1]
PATTERN=re.compile(r'^\d+\.\d+\.\d+\.\d+$')

def as_parts(value):
    if not isinstance(value,str) or not PATTERN.fullmatch(value):
        raise ValueError(f'Nieprawidlowa wersja: {value}')
    return tuple(int(x) for x in value.split('.'))

def next_version(current,published):
    current_parts=as_parts(current)
    unique={as_parts(x) for x in published}
    if not unique or current_parts>max(unique):
        # A prepared but not yet published version must not be skipped on retry.
        return current
    highest=max(unique|{current_parts})
    major,minor,patch,_=highest
    return f'{major}.{minor}.{patch+1}.0'

def published_versions(root):
    meta=json.loads((root/'meta.json').read_text(encoding='utf-8'))
    manifest=json.loads((root/'manifest.json').read_text(encoding='utf-8'))
    result=[]
    for entry in manifest:
        if entry.get('guid','').lower()==meta['guid'].lower():
            result.extend(str(v['version']) for v in entry.get('versions',[]))
    try:
        tags=subprocess.check_output(['git','tag','-l','v*'],cwd=root,text=True,stderr=subprocess.DEVNULL).splitlines()
    except (FileNotFoundError,subprocess.CalledProcessError):
        tags=[]
    for tag in tags:
        if PATTERN.fullmatch(tag[1:]): result.append(tag[1:])
    return result

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--write',action='store_true')
    args=parser.parse_args()
    metadata_path=ROOT/'meta.json'
    metadata=json.loads(metadata_path.read_text(encoding='utf-8'))
    proposed=next_version(metadata['version'],published_versions(ROOT))
    if args.write:
        metadata['version']=proposed
        metadata_path.write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('NEXT_VERSION='+proposed)
    if args.write: print('Przygotowano wersje',proposed)

if __name__=='__main__':main()
