"""Contract checks for the consolidated UI and authenticated device registry."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
P=ROOT/'src/Jellyfin.Plugin.MisioCache'
class SharedSmartDownloadsTests(unittest.TestCase):
    def test_single_settings_and_native_version_navigation(self):
        html=(P/'Web/configPage.html').read_text(encoding='utf-8')
        versions=(P/'Web/versionsPage.html').read_text(encoding='utf-8')
        plugin=(P/'Plugin.cs').read_text(encoding='utf-8')
        for required in ('mc-smart-prefs-panel','mc-smart-devices-panel','mc-smart-download-panel',
                         'mc-settings-updates-panel','mc-devices-list','mc-smart-batch','mc-update-install'):
            self.assertIn(required,html)
        self.assertEqual(plugin.count('EnableInMainMenu = true'),2)
        self.assertIn('DisplayName = "MisioCache — ustawienia"',plugin)
        self.assertIn('DisplayName = "MisioCache"',plugin)
        self.assertIn('MisioCacheVersions',plugin)
        self.assertIn('5cf368285b7b4d18a28e1e87922e505b?name=MisioCache',versions)
        self.assertIn('MisioCacheSmartDownloads.saveSettings',html)
        self.assertIn('MisioCacheSmartDownloads.manageDevice',html)
    def test_registry_auth_and_persistence(self):
        controller=(P/'Controllers/MisioCacheSmartDevicesController.cs').read_text(encoding='utf-8')
        store=(P/'Services/MisioCacheSmartDeviceStore.cs').read_text(encoding='utf-8')
        services=(P/'Services/MisioCacheServiceRegistrator.cs').read_text(encoding='utf-8')
        self.assertIn('[Authorize]',controller)
        self.assertIn('Policies.RequiresElevation',controller)
        self.assertIn('Jellyfin-UserId',controller)
        self.assertIn('Jellyfin-DeviceId',controller)
        self.assertIn('User.Claims',controller)
        self.assertNotIn('userId = body.UserId',controller)
        self.assertIn('Path.Combine(paths.DataPath',store)
        self.assertIn('File.Move(temp, _path, true)',store)
        self.assertIn('x.UserId == user && x.DeviceId == device',store)
        self.assertIn('services.AddSingleton<MisioCacheSmartDeviceStore>()',services)
    def test_native_hook_reads_server_and_records_status(self):
        script=(P/'Web/smart-downloads.js').read_text(encoding='utf-8')
        for word in ('await getSettings()','request(\'current?nativeSupported=',
                     "request('current/status'","request('current/hook'",'saveSettings, listDevices, manageDevice'):
            self.assertIn(word,script)
        self.assertIn('const result = original.apply(this, arguments)',script)
        self.assertIn('suppressHook++',script)
        self.assertNotIn('cacheStorage.open',script)
if __name__=='__main__':unittest.main()
