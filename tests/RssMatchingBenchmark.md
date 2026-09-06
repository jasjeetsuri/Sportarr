# RSS Matching Baseline

`RssMatchingBenchmarkTests` calls the existing private `RssSyncService.FindMatchingEvent`
method through a bound delegate. It exercises title parsing, per-event validation,
and best-event selection without changing production visibility or implementing a
second matching loop. A selector rename/signature change requires updating the binding.

## Run

From the repository root, run the small correctness case (20 releases, 71 events):

```sh
dotnet test tests/Sportarr.Api.Tests/Sportarr.Api.Tests.csproj -c Release -p:SkipFrontendBuild=true --filter FullyQualifiedName~RssMatchingBenchmarkTests --logger 'console;verbosity=detailed'
```

For the full 900-release, 71-event baseline on macOS/Linux:

```sh
SPORTARR_RSS_BENCHMARK=1 dotnet test tests/Sportarr.Api.Tests/Sportarr.Api.Tests.csproj -c Release -p:SkipFrontendBuild=true --filter FullyQualifiedName~RssMatchingBenchmarkTests --logger 'console;verbosity=detailed'
```

On PowerShell, set `$env:SPORTARR_RSS_BENCHMARK = '1'` before the same `dotnet test`
command. Remove that variable afterward to restore the smaller default workload.
Run in a fresh test process for comparisons, on the same hardware/build/runtime.
Use the narrow filter above; the collection disables concurrent test collections.

## Workload and Assertions

- 71 synthetic monitored F1 session events, using fixed historical dates (not a real calendar).
- 900 unique release titles in full mode, using ten repeating naming patterns with distinct groups.
- 30% expected F1 matches, including country/demonym naming and quality variants.
- 70% unrelated sport/TV/movie or wrong-year releases with no expected event.
- 63,900 release/event comparisons per full pass; both passes assert all expected event IDs.
- First and repeat passes share existing parser/normalization caches. "First" is not a
  guaranteed cold-cache measurement if other matching tests ran in the process.
- No timing threshold: the test gates correctness, while emitted measurements support
  before/after comparisons without flaky machine-dependent assertions.

## Initial Measurement

Measured on macOS x64, .NET SDK 8.0.424/runtime 8.0.30, Release configuration,
against upstream main `3cfbf7077`. One run, not a statistical benchmark:

| Pass | Wall Time | Process CPU Time | Thread Allocated Bytes | Matches |
| --- | ---: | ---: | ---: | ---: |
| First | 62.451 s | 62.085 s | 28,332,635,688 | 270 |
| Repeat | 57.991 s | 56.140 s | 28,279,910,752 | 270 |

Allocated bytes are cumulative allocations on the synchronous matching thread,
not retained heap size or peak memory. Process CPU includes runtime/GC work and
any other activity in the test process. No forced garbage collection is performed.

This is a selector workload, not the entire RSS sync: it excludes HTTP/feed parsing,
database candidate queries, publication-age filtering, quality/upgrade decisions,
pending releases, download submission, and production logging. The 900-by-71 shape
matches the investigated workload size; the synthetic titles are not a production
feed replay. These numbers do not establish individual method hotspots or predict
ARM VPS timings. Capture managed CPU/allocation profiles before choosing an optimization.