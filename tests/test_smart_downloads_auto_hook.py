"""Independent MisioCache client hook: native download integration, no MisioFlix code."""
import shutil
import subprocess
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'src/Jellyfin.Plugin.MisioCache'


class NativeDownloadQueueTests(unittest.TestCase):
    def test_own_asset_and_index_only_loader(self):
        js = (SRC / 'Web/smart-downloads.js').read_text(encoding='utf-8')
        loader = (SRC / 'Services/MisioCacheSmartDownloadsStartupFilter.cs').read_text(encoding='utf-8')
        assets = (SRC / 'Controllers/MisioCacheClientAssetsController.cs').read_text(encoding='utf-8')
        reg = (SRC / 'Services/MisioCacheServiceRegistrator.cs').read_text(encoding='utf-8')
        self.assertIn('MisioCache/assets/smart-downloads.js', loader)
        self.assertIn('IsIndex(context.Request.Path.Value)', loader)
        self.assertIn('data-misiocache', loader)
        self.assertIn('Jellyfin.Plugin.MisioCache.Web.smart-downloads.js', assets)
        self.assertIn('AddTransient<IStartupFilter, MisioCacheSmartDownloadsStartupFilter>', reg)
        self.assertNotIn('MisioCachePassiveHttpStartupFilter', reg)
        for file in (js, loader, assets):
            self.assertNotIn('Jellyfin.Plugin.MisioFlix', file)
        self.assertIn('downloadManual', js)
        self.assertIn('NativeShell', js)
        self.assertIn('enabled !== true', js)

    @unittest.skipUnless(shutil.which('node'), 'Node.js not installed')
    def test_native_bridge_and_opt_in_behavior(self):
        result = subprocess.run(
            ['node', str(ROOT / 'tests/js/test_smart_downloads_auto_hook.cjs')],
            cwd=ROOT, capture_output=True, text=True, timeout=30, check=False
        )
        self.assertEqual(result.returncode, 0, result.stdout + '\n' + result.stderr)
        self.assertIn('opt-in', result.stdout)


if __name__ == '__main__':
    unittest.main()
