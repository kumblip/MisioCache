"""Source gates for new dashboard and opt-in response-body measurement.
C# production logic is verified separately via tests/dotnet/Program.cs in CI.
"""
from pathlib import Path
import subprocess
import shutil
import unittest

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'src/Jellyfin.Plugin.MisioCache'

class DenseTelemetryTests(unittest.TestCase):
    def test_compact_dashboard_is_first_and_has_metrics(self):
        html=(SRC/'Web/configPage.html').read_text(encoding='utf-8')
        self.assertLess(html.index('id="mc-observation"'),html.index('id="mc-active-heading"'))
        for field in ('mc-kpi-forward','mc-kpi-backward','mc-kpi-realrepeat',
                      'mc-kpi-unknown','mc-kpi-accepted','mc-kpi-mbps',
                      'mc-kpi-p95','mc-kpi-first','mc-kpi-maxjump','mc-compact-recent'):
            self.assertEqual(html.count('id="'+field+'"'),1,field)
        self.assertIn('class="mc-dashboard"',html)
        self.assertEqual(html.count('id="mc-meter-optin"'),1)
        self.assertIn('EnableResponseBodyByteMeter',html)
        self.assertIn('class="mc-details"',html)
        self.assertIn('drawCompact(d)',html)
        self.assertIn('replaceChildren()',html)
        self.assertIn('td.textContent=String(value)',html)
    def test_feature_is_pass_through_and_counts_downstream_writes(self):
        meter=(SRC/'Services/MisioCacheResponseByteMeter.cs').read_text(encoding='utf-8')
        middleware=(SRC/'Services/MisioCachePassiveHttpStartupFilter.cs').read_text(encoding='utf-8')
        for part in ('_inner.WriteAsync(', '_inner.Write(', '_inner.Advance(',
                     '_inner.SendFileAsync(', 'IHttpResponseBodyFeature',
                     'MarkUnverifiedSendfile', 'Interlocked.Add'):
            self.assertIn(part,meter)
        self.assertNotIn('new FileStream(',meter)
        self.assertNotIn('CopyToAsync',meter)
        self.assertNotIn('Response.Body =',middleware)
        self.assertIn('context.Features.Set<IHttpResponseBodyFeature>(wrapper)',middleware)
        self.assertIn('context.Features.Set(originalFeature)',middleware)
        self.assertIn('catch { /* telemetry must never break native playback */ }',middleware)
    def test_http_dashboard_js_syntax(self):
        if not shutil.which('node'):self.skipTest('node not installed')
        html=(SRC/'Web/configPage.html').read_text(encoding='utf-8')
        scripts=html.split('<script>')[1].split('</script>')[0]
        r=subprocess.run(['node','--check','-'],input=scripts,text=True,capture_output=True)
        self.assertEqual(r.returncode,0,r.stderr)

if __name__=='__main__':unittest.main()
