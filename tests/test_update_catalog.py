"""Checks feature parity and security of admin-only native update catalog refresh."""
from pathlib import Path
import re
import unittest
import subprocess
import shutil

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'src/Jellyfin.Plugin.MisioCache'

class UpdateCatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.ui=(SRC/'Web/configPage.html').read_text(encoding='utf-8')
        cls.config=(SRC/'Configuration/PluginConfiguration.cs').read_text(encoding='utf-8')
        cls.reg=(SRC/'Services/MisioCacheServiceRegistrator.cs').read_text(encoding='utf-8')
        cls.svc=(SRC/'Services/MisioCacheNativeCatalogRefreshService.cs').read_text(encoding='utf-8')
        cls.status=(SRC/'Services/MisioCacheUpdateService.cs').read_text(encoding='utf-8')
        cls.ctrl=(SRC/'Controllers/MisioCacheUpdateController.cs').read_text(encoding='utf-8')

    def test_controls_are_present(self):
        for identifier in ('mc-update-installed','mc-update-github','mc-update-manifest',
                           'mc-update-catalog','mc-update-checked','mc-update-check','mc-update-install',
                           'mc-update-auto','mc-update-status'):
            self.assertIn('id="'+identifier+'"',self.ui)
        self.assertIn('Wymuś aktualizację katalogu Jellyfina',self.ui)
        self.assertIn('Zainstaluj aktualizację',self.ui)
    def test_control_routes_native(self):
        self.assertIn("query('MisioCache/api/updates/native-catalog/refresh','POST')",self.ui)
        self.assertIn("query('Packages/Installed/MisioCache?'",self.ui)
        self.assertIn("query('Packages')",self.ui)
        self.assertIn("query('Repositories')",self.ui)
        self.assertIn("query('Plugins')",self.ui)
    def test_security_policy(self):
        self.assertIn('Policies.RequiresElevation',self.ctrl)
        self.assertEqual(self.ctrl.count('[Authorize(Policy = Policies.RequiresElevation)]'),2)
        self.assertIn('[HttpPost("native-catalog/refresh")]',self.ctrl)
        self.assertNotIn('System.IO.File.WriteAllBytes',self.ctrl)
    def test_isolated_from_misioflix(self):
        for source in (self.ui,self.config,self.reg,self.svc,self.status,self.ctrl):
            self.assertNotIn('Jellyfin.Plugin.MisioFlix',source)
            self.assertNotIn('97d15b37-b868-492b-83da-eb67b16a0d44',source)
        self.assertIn('5cf36828-5b7b-4d18-a28e-1e87922e505b',self.svc)
    def test_verified_immutable_revision(self):
        for literal in ('commits/main','/manifest.json','ShaPattern','ReadCurrentCommitAsync',
                        'GetPackages(', 'ReadVerifiedVersionAsync','12.4.0.0','SourceUrl',
                        'SaveConfiguration()', 'matching.Length != 1'):
            self.assertIn(literal,self.svc)
        self.assertIn('RepositoryVersion',self.svc)
        self.assertIn('VerifiedByJellyfin',self.svc)
    def test_hosted_refresh_optional_install(self):
        self.assertIn('AddHostedService<MisioCacheUpdateService>',self.reg)
        self.assertIn('AddSingleton<MisioCacheNativeCatalogRefreshService>',self.reg)
        self.assertIn('TimeSpan.FromMinutes(15)',self.status)
        self.assertIn('public bool AutoCheckUpdates',self.config)
        self.assertIn('compare(manifest,installed)>0',self.ui)
        self.assertIn('window.confirm(',self.ui)
    def test_reported_versions_and_compatibility(self):
        for property_name in ('InstalledVersion','GitHubVersion','RepositoryVersion','RepositoryTargetAbi','CheckedAt'):
            self.assertIn(property_name,self.status)
        self.assertIn('12.4.0.0',self.ui)
        self.assertIn('raw.githubusercontent.com/kumblip/MisioCache',self.ui)
        self.assertIn('github.com/kumblip/MisioCache',self.svc)
    @unittest.skipUnless(shutil.which('node'), 'Node.js niedostępny')
    def test_runtime_panel_flow(self):
        task=subprocess.run(['node',str(ROOT/'tests/js/test_update_panel.cjs')],
                            cwd=ROOT,text=True,capture_output=True,timeout=40)
        self.assertEqual(task.returncode,0,task.stdout+'\n'+task.stderr)
        self.assertIn('native install',task.stdout)

    def test_has_no_direct_plugin_binary_update(self):
        for forbidden in ('File.WriteAllBytes','File.Copy','File.Move','File.Delete','ServiceWorker',
                          'IStartupFilter','UseMiddleware','/web/index.html'):
            self.assertNotIn(forbidden,self.svc)
            self.assertNotIn(forbidden,self.reg)

if __name__=='__main__':unittest.main()
