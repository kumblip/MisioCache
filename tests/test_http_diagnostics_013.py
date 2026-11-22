"""Retirement checks for legacy HTTP telemetry; kept under the original filename for CI discovery."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'src/Jellyfin.Plugin.MisioCache'
class NoBackgroundTelemetryTests(unittest.TestCase):
    def test_middleware_not_registered(self):
        r=(SRC/'Services/MisioCacheServiceRegistrator.cs').read_text(encoding='utf-8')
        for name in ('IStartupFilter','MisioCachePassiveHttpStartupFilter','MisioCacheHttpObservationStore','MisioCacheResponseByteMeter'):
            self.assertNotIn(name,r)
        self.assertIn('MisioCacheUpdateService',r)
    def test_no_diagnostics_endpoint_or_instrumentation(self):
        files=('Controllers/MisioCacheDiagnosticsController.cs','Services/MisioCachePassiveHttpStartupFilter.cs',
               'Services/MisioCacheHttpObservationStore.cs','Services/MisioCacheResponseByteMeter.cs')
        for filename in files:
            content=(SRC/filename).read_text(encoding='utf-8')
            for part in ('[ApiController]','IStartupFilter','IHttpResponseBodyFeature','app.Use(','[HttpGet(', '[HttpPost(', 'UseMiddleware'):
                self.assertNotIn(part,content,filename)
    def test_old_switches_and_cache_policy_removed(self):
        page=(SRC/'Web/configPage.html').read_text(encoding='utf-8')
        for value in ('mc-http-optin','mc-meter-optin','mc-observation','mc-policy-form',
                      'mc-cache-storage','mc-kpi-',"query('Sessions')"):
            self.assertNotIn(value,page)
        self.assertIn('mc-updates-panel',page)
if __name__=='__main__':unittest.main()
