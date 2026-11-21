"""v0.1.4: source/host checks + real DOM panel mock; native .NET engine is
exercised by tests/dotnet on GitHub Actions, not falsely simulated here.
"""
import json
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'src/Jellyfin.Plugin.MisioCache'
read=lambda path:path.read_text(encoding='utf-8')

class ByteRangeTelemetryTests(unittest.TestCase):
    def test_source_version_matches_current_release(self):
        meta=json.loads(read(ROOT/'meta.json'))
        self.assertRegex(meta['version'],r'^0\.1\.\d+\.0$')
        self.assertIn(meta['version'],read(SRC/'Jellyfin.Plugin.MisioCache.csproj'))
        self.assertIn('mc-byte-recent',read(SRC/'Web/configPage.html'))

    def test_numeric_only_range_parsing_with_safety_limits(self):
        source=read(SRC/'Services/MisioCacheHttpObservationStore.cs')
        for token in ('TryParseContentRange','IsSingleByteRequest','RegexMatchTimeoutException',
                      'raw.Length > 128','raw.Length > 100','last < total','NumberStyles.None',
                      'last == long.MaxValue','isHead','if (!isHead'):
            self.assertIn(token,source)
        self.assertIn('Content-Range',read(SRC/'Services/MisioCachePassiveHttpStartupFilter.cs'))

    def test_anonymous_media_correlation_not_identifier_storage(self):
        store=read(SRC/'Services/MisioCacheHttpObservationStore.cs')
        filter_=read(SRC/'Services/MisioCachePassiveHttpStartupFilter.cs')
        for needle in ('RandomNumberGenerator.GetBytes(32)','HMACSHA256.HashData','TransientMediaTag',
                       'DateTimeOffset.UtcNow','TimeSpan.FromMinutes(30)','MaxSamples = 2048',
                       'recentRanges = recent.TakeLast(MaxRecent).ToArray()'):
            self.assertIn(needle,store)
        self.assertIn('TryExtractMediaId',filter_)
        self.assertNotIn('context.Request.Query',filter_)
        self.assertNotIn('context.Response.Body',filter_)
        self.assertNotIn('context.Request.Body',filter_)
        for forbidden in ('AccessToken','RemoteIpAddress','Authorization','UserId','ItemId','RequestPath','FilePath'):
            self.assertNotIn(forbidden,store)

    def test_overlap_per_media_and_declared_not_actual_bytes(self):
        store=read(SRC/'Services/MisioCacheHttpObservationStore.cs')
        for token in ('Dictionary<string, List<(long Start, long End)>>','OverlapAndInsert',
                      'declaredPartialBytes = declared','declaredUniqueBytes = declared - declaredRepeated',
                      'declaredRepeatedBytes = declaredRepeated','overlappingResponses',
                      'measuredBytes = (long?)null'):
            self.assertIn(token,store)
        html=read(SRC/'Web/configPage.html')
        for identifier in ('mc-byte-total','mc-byte-unique','mc-byte-repeated','mc-byte-recent',
                           'mc-byte-parsed','mc-byte-unparsed','mc-byte-overlap-count'):
            self.assertIn('id="'+identifier+'"',html)
        self.assertIn('Do testu powtórzeń nie zeruj liczników',html)
        self.assertIn('td.textContent=value',html)
        self.assertNotIn('tr.innerHTML',html)

    def test_git_actions_compiles_real_range_engine_before_release(self):
        proj=read(ROOT/'tests/dotnet/MisioCache.RangeEngineTests.csproj')
        self.assertIn('MisioCacheHttpObservationStore.cs',proj)
        checks=read(ROOT/'tests/dotnet/Program.cs')
        for token in ('declaredRepeatedBytes','anonymousMediaCount','full duplicate',
                      'HEAD ignored','bad ranges do not change overlap','reset removes history'):
            self.assertIn(token,checks)
        # CI invokes run_tests.py in BOTH workflows already. C# test projects
        # are automatically discovered there, without changing the workflows.
        runner=read(ROOT/'tools/run_tests.py')
        self.assertIn('rglob("*.csproj")', runner)
        self.assertIn('subprocess.run(', runner)
        self.assertIn('GITHUB_ACTIONS', runner)
        for action in ('build.yml','release.yml'):
            self.assertIn('python3 tools/run_tests.py', read(ROOT/'.github/workflows'/action))

    @unittest.skipUnless(shutil.which('node'), 'Node.js nie jest dostępny')
    def test_panel_runtime_range_rows_and_repeated_bytes(self):
        job=subprocess.run(['node',str(ROOT/'tests/js/test_byte_ranges_014.cjs')],
                           cwd=ROOT,capture_output=True,text=True,timeout=40)
        self.assertEqual(job.returncode,0,job.stdout+'\n'+job.stderr)
        self.assertIn('nakładanie',job.stdout)

if __name__=='__main__':unittest.main()
