"""v0.1.3 passive HTTP diagnostics compatibility and safety checks.

The C# runtime still requires the .NET 10/Jellyfin CI build; these tests do not
pretend to execute Jellyfin's HTTP pipeline locally.
"""
import json
from pathlib import Path
import re
import unittest
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'src/Jellyfin.Plugin.MisioCache'
read=lambda p:p.read_text(encoding='utf-8')

class PassiveDiagnosticsTests(unittest.TestCase):
    def test_diagnostic_enabled_only_by_opt_in(self):
        cfg=read(SRC/'Configuration/PluginConfiguration.cs')
        self.assertIn('EnablePassiveHttpDiagnostics { get; set; } = false;',cfg)
        html=read(SRC/'Web/configPage.html')
        self.assertIn('current.EnablePassiveHttpDiagnostics=field.checked',html)
        self.assertIn('ApiClient.updatePluginConfiguration(pluginId,current)',html)

    def test_pipeline_pass_through_always_calls_native_player(self):
        code=read(SRC/'Services/MisioCachePassiveHttpStartupFilter.cs')
        self.assertIn('IStartupFilter',code)
        self.assertIn('await nextRequest().ConfigureAwait(false)',code)
        self.assertIn('finally',code)
        self.assertIn('context.Response.StatusCode',code)
        self.assertIn('context.Request.Headers.ContainsKey("Range")',code)
        self.assertIn('context.Response.Headers.ContainsKey("Content-Range")',code)
        self.assertIn('TryClassify(path, out var kind)',code)
        self.assertNotIn('context.Request.Query',code)
        self.assertNotIn('context.Request.Body',code)
        self.assertNotIn('context.Response.Body',code)
        self.assertNotIn('UseResponseCompression',code)

    def test_does_not_store_urls_or_secrets(self):
        code=read(SRC/'Services/MisioCacheHttpObservationStore.cs')
        self.assertIn('MaxSamples = 2048',code)
        self.assertIn('TimeSpan.FromMinutes(30)',code)
        self.assertIn('DateTimeOffset.UtcNow',code)
        self.assertIn('long?)null',code)
        forbidden=('AccessToken','Authorization','RequestPath','RemoteIpAddress','UserId','ItemId','FilePath','QueryString')
        for name in forbidden:
            self.assertNotIn(name,code)

    def test_admin_only_diagnostics_endpoints(self):
        code=read(SRC/'Controllers/MisioCacheDiagnosticsController.cs')
        self.assertIn('[Authorize(Policy = Policies.RequiresElevation)]',code)
        self.assertIn('[HttpGet("http")]',code)
        self.assertIn('[HttpPost("http/reset")]',code)
        self.assertIn('private, no-store',code)
        self.assertNotIn('SendFile',code)

    def test_startup_registers_only_diagnostics_not_player(self):
        code=read(SRC/'Services/MisioCacheServiceRegistrator.cs')
        self.assertIn('services.AddSingleton<MisioCacheHttpObservationStore>()',code)
        self.assertIn('services.AddTransient<IStartupFilter, MisioCachePassiveHttpStartupFilter>()',code)
        for illegal in ('MediaSource','PlaybackManager','FFmpeg','CacheDataSource'):
            self.assertNotIn(illegal,code)

    def test_uses_real_jellyfin_sessions_not_invented_playmode(self):
        page=read(SRC/'Web/configPage.html')
        self.assertIn("request('Sessions')",page)
        self.assertIn("'PlayMethod','playMethod'",page)
        self.assertIn('Nieznany',page)
        self.assertIn('textContent=name+',page)
        self.assertNotIn('li.innerHTML',page)
        self.assertIn('mc-http-206',page)
        self.assertIn('mc-http-valid',page)
        self.assertIn('mc-http-ignored',page)
        self.assertIn('mc-http-types',page)

    @unittest.skipUnless(shutil.which('node'), 'Node.js niedostępny')
    def test_runtime_diagnostics_ui(self):
        task=subprocess.run(['node',str(ROOT/'tests/js/test_http_diagnostics_013.cjs')],
            cwd=ROOT,text=True,capture_output=True,timeout=30)
        self.assertEqual(task.returncode,0,task.stdout+'\n'+task.stderr)
        self.assertIn('opt-in + reset',task.stdout)

    def test_version_is_labeled_and_assembly_synced(self):
        meta=json.loads(read(ROOT/'meta.json'))
        self.assertEqual(meta['version'],'0.1.3.0')
        project=ET.fromstring(read(SRC/'Jellyfin.Plugin.MisioCache.csproj'))
        self.assertEqual(project.findtext('./PropertyGroup/AssemblyVersion'),'0.1.3.0')
        self.assertEqual(project.findtext('./PropertyGroup/FileVersion'),'0.1.3.0')

    def test_auto_release_is_unfiltered_main(self):
        release=read(ROOT/'.github/workflows/release.yml')
        self.assertIn('    branches: [main]',release)
        self.assertNotIn('    paths:',release)
        self.assertIn('tools/next_release.py --write',release)
        self.assertIn('gh release create',release)

    def test_no_cache_or_video_interception(self):
        http=read(SRC/'Services/MisioCachePassiveHttpStartupFilter.cs')
        page=read(SRC/'Web/configPage.html')
        self.assertNotIn('serviceWorker.register(',page)
        self.assertNotIn('video.src',page)
        self.assertNotIn('context.Request.Body',http)
        self.assertNotIn('context.Response.Body',http)
        self.assertIn('nie zapisuje jeszcze filmu w cache',page)

if __name__=='__main__':unittest.main()
