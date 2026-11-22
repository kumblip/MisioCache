"""Old HTTP-range tests replaced with guards that the abandoned feature stays removed."""
from pathlib import Path
import subprocess,shutil,unittest
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'src/Jellyfin.Plugin.MisioCache'
class CleanSmartDownloadsTests(unittest.TestCase):
    def test_individual_preferences_remain_in_phone_interface(self):
        h=(SRC/'Web/overviewPage.html').read_text(encoding='utf-8')
        for key in ('NativeShell.downloadFiles','mc-smart-enabled','mc-smart-budget','mc-smart-count',
                    'preferenceKey','budgetGB','count:2'):
            self.assertIn(key,h)
    def test_diagnostics_are_not_in_browser_or_config(self):
        html=(SRC/'Web/configPage.html').read_text(encoding='utf-8')
        self.assertIn('MisioCache — ustawienia',html)
        self.assertIn('Aktualizacje MisioCache',html)
        for key in ('Stan obserwatora','Suma deklarowanych','Pomiar bajtów','Diagnostyka obecnego urządzenia'):
            self.assertNotIn(key,html)
    @unittest.skipUnless(shutil.which('node'),'Node not installed')
    def test_smart_downloads_js(self):
        p=subprocess.run(['node',str(ROOT/'tests/js/test_smart_downloads_017.cjs')],capture_output=True,text=True,timeout=45)
        self.assertEqual(p.returncode,0,p.stderr+p.stdout)
if __name__=='__main__':unittest.main()
