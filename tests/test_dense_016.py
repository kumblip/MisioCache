"""Check that only update controls remain in server settings."""
from pathlib import Path
import shutil,subprocess,unittest
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'src/Jellyfin.Plugin.MisioCache'
class SlimSettingsTests(unittest.TestCase):
    def test_standalone_updates_panel(self):
        html=(SRC/'Web/configPage.html').read_text(encoding='utf-8')
        for key in ('mc-update-install','mc-update-check','mc-update-auto','AutoCheckUpdates'):
            self.assertIn(key,html)
        for banned in ('mc-http-','mc-kpi-','mc-policy-form','mc-quota'):
            self.assertNotIn(banned,html)
    @unittest.skipUnless(shutil.which('node'),'Node not installed')
    def test_settings_js_syntax(self):
        html=(SRC/'Web/configPage.html').read_text(encoding='utf-8')
        js=html.split('<script>')[1].split('</script>')[0]
        p=subprocess.run(['node','--check','-'],input=js,capture_output=True,text=True,timeout=30)
        self.assertEqual(p.returncode,0,p.stderr)
if __name__=='__main__':unittest.main()
