import hashlib
import json
from pathlib import Path
import re
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import next_release
import update_manifest

class MisioCacheFirstReleaseTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.meta=json.loads((ROOT/'meta.json').read_text(encoding='utf-8'))
        cls.project=(ROOT/'src/Jellyfin.Plugin.MisioCache/Jellyfin.Plugin.MisioCache.csproj').read_text(encoding='utf-8')
        cls.plugin=(ROOT/'src/Jellyfin.Plugin.MisioCache/Plugin.cs').read_text(encoding='utf-8')
        cls.settings=(ROOT/'src/Jellyfin.Plugin.MisioCache/Web/configPage.html').read_text(encoding='utf-8')
        cls.release=(ROOT/'.github/workflows/release.yml').read_text(encoding='utf-8')

    def test_metadata(self):
        self.assertRegex(self.meta['version'],r'^\d+\.\d+\.\d+\.\d+$')
        self.assertEqual(self.meta['targetAbi'],'12.4.0.0')
        self.assertEqual(self.meta['assemblies'],['Jellyfin.Plugin.MisioCache.dll'])
        self.assertEqual(self.meta['owner'],'kumblip')
    def test_assembly_version_matches(self):
        tree=ET.fromstring(self.project)
        self.assertEqual(tree.findtext('./PropertyGroup/AssemblyVersion'),self.meta['version'])
        self.assertEqual(tree.findtext('./PropertyGroup/FileVersion'),self.meta['version'])
    def test_separate_plugin_guid(self):
        self.assertIn(self.meta['guid'],self.plugin)
        self.assertNotIn('97d15b37-b868-492b-83da-eb67b16a0d44',self.plugin)
    def test_two_native_menu_entries(self):
        self.assertEqual(self.plugin.count('EnableInMainMenu = true'),2)
        self.assertIn('MisioCacheOverview',self.plugin)
        self.assertIn('MisioCache — ustawienia',self.plugin)
    def test_settings_saved_using_jellyfin_api(self):
        self.assertIn('ApiClient.getPluginConfiguration(pluginId)',self.settings)
        self.assertIn('ApiClient.updatePluginConfiguration(pluginId,c)',self.settings)
        for field in ('DownloadUntilEnd','PreserveWatchedPartsUntilCompletion','DownloadWhilePaused',
                      'DeleteAfterCompleted','KeepAfterStopMinutes','PlannedMaxDeviceCacheGiB'):
            self.assertIn(field,self.settings)
    def test_no_playback_hooks(self):
        for needle in ('playbackManager','fetch(', 'XMLHttpRequest(', 'serviceWorker.register(', 'CacheDataSource', 'new MediaSource('):
            self.assertNotIn(needle,self.settings)
        self.assertIn('nie zapisuje jeszcze filmu w cache',self.settings)
    def test_auto_release_on_commit(self):
        self.assertIn('  push:\n',self.release)
        self.assertIn('    branches: [main]',self.release)
        self.assertIn('dotnet publish',self.release)
        self.assertIn('tools/run_tests.py',self.release)
        self.assertIn('tools/next_release.py --write',self.release)
        self.assertIn('gh release create',self.release)
        self.assertIn('tools/update_manifest.py',self.release)
        self.assertIn('git push origin HEAD:main',self.release)
    def test_source_restricted_to_jellyfin_server(self):
        self.assertIn('Jellyfin.Controller',self.project)
        self.assertIn('Jellyfin.Model',self.project)
        self.assertIn('net10.0',self.project)
        self.assertNotIn('MisioFlix',self.project)
    def test_auto_version_initial(self):
        self.assertEqual(next_release.next_version('0.1.0.0',[]),'0.1.0.0')
    def test_auto_version_next_and_retry(self):
        self.assertEqual(next_release.next_version('0.1.0.0',['0.1.0.0']),'0.1.1.0')
        self.assertEqual(next_release.next_version('0.1.1.0',['0.1.0.0']),'0.1.1.0')
        self.assertEqual(next_release.next_version('0.1.1.0',['0.1.0.0','0.1.1.0']),'0.1.2.0')
    def test_version_rejects_malformed(self):
        for invalid in ('v0.1.0.0','0.1.0','0.1.-1.0','0.1.0.0.0'):
            with self.assertRaises(ValueError):next_release.as_parts(invalid)
    def test_manifest_checksum_and_url(self):
        with tempfile.TemporaryDirectory() as d:
            td=Path(d)
            meta=td/'meta.json';meta.write_text(json.dumps(self.meta),encoding='utf-8')
            manifest=td/'manifest.json';manifest.write_text('[]',encoding='utf-8')
            archive=td/f'MisioCache_{self.meta["version"]}_JF12.4.zip'
            with zipfile.ZipFile(archive,'w') as z:
                z.writestr('meta.json',json.dumps(self.meta))
                z.writestr('Jellyfin.Plugin.MisioCache.dll',b'fake-dll-for-metadata-test')
            entries=update_manifest.update(archive,meta,manifest,'kumblip/MisioCache',stamp='2026-11-21T00:00:00Z')
            entry=entries[0]['versions'][0]
            self.assertEqual(entry['checksum'],hashlib.md5(archive.read_bytes()).hexdigest())
            self.assertIn('/releases/download/v0.1.0.0/',entry['sourceUrl'])
            self.assertEqual(entry['targetAbi'],'12.4.0.0')
            with self.assertRaises(ValueError):
                update_manifest.update(archive,meta,manifest,'kumblip/MisioCache')
    def test_manifest_unchanged_before_first_release(self):
        manifest=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
        self.assertIsInstance(manifest,list)
    def test_no_misioflix_source_files(self):
        for p in (ROOT/'src').rglob('*.cs'):
            self.assertNotIn('Jellyfin.Plugin.MisioFlix',p.read_text(encoding='utf-8'))

if __name__=='__main__':unittest.main()
