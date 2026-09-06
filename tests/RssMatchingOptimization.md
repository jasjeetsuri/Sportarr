# Motorsport Regex Reuse

Optimization based on [the RSS matching baseline](RssMatchingBenchmark.md).
No RSS selection rules, monitoring settings, or download behavior were changed.

## Profiling Evidence

The existing benchmark was invoked through a temporary executable outside the
repository, retaining its expected-event assertions. `dotnet-trace` 8.0.547301
captured CPU samples and CLR GC allocation events on .NET 8.0.30/macOS x64:

```sh
dotnet-trace collect --profile cpu-sampling --providers 'Microsoft-Windows-DotNETRuntime:0x40000001:5' --output baseline.nettrace --show-child-io -- dotnet <runner.dll>
```

TraceEvent 3.1.21 was used outside the repository to attribute allocation ticks
and CPU samples to the nearest Sportarr service frame. This added no application
or test-project dependency. Allocation ticks are sampled estimates, not exact
per-method allocation counters; inclusive CPU percentages must not be added.

The baseline trace attributed 80.8% of CPU samples and 92.7% of sampled allocation
bytes to three `EventPartDetector` methods: filename session detection, event
session detection, and session normalization. Stacks showed repeated regex parsing
and construction through the framework's static regex cache, whose default capacity
is 15 patterns. The detector cycles through substantially more fixed patterns.

## Change

A private concurrent cache reuses regex instances by pattern, options, and current
culture across those three methods. It preserves pattern text, matching order,
options, and existing fallback behavior. Keys come only from internal pattern
tables and literals, not release titles. The framework's global cache is unchanged.
Different cultures have separate entries; regex instances are safe for concurrent
matching. This does not add a cache of release decisions or database state.

## Untraced Results

Same Release-build test, 900 releases, 71 events, 63,900 comparisons per pass:

| Metric | Baseline First | Optimized First | Baseline Repeat | Optimized Repeat |
| --- | ---: | ---: | ---: | ---: |
| Wall time | 62.451 s | 12.171 s | 57.991 s | 9.741 s |
| Process CPU time | 62.085 s | 12.930 s | 56.140 s | 9.548 s |
| Thread allocated bytes | 28,332,635,688 | 2,067,482,920 | 28,279,910,752 | 2,025,720,032 |
| Expected matches | 270 | 270 | 270 | 270 |

This run was approximately 5-6 times faster, with about 93% fewer cumulative
allocations. These are individual local measurements, not statistical confidence
intervals or an ARM/VPS performance guarantee. The synthetic corpus and exclusions
in the baseline document still apply. Allocation totals are not retained memory.

The follow-up trace attributed about 46.6 MB to filename session detection across
both passes, versus 38.7 GB before. Regex construction no longer dominated the CPU
summary. Other normalization, sport detection, and location checks remain expensive;
they were intentionally left outside this change. Tracing adds overhead, so the
table above uses only untraced test runs.

## Verification

- All expected event IDs matched on both full benchmark passes.
- 507 matching/session/event-part/parser/RSS/normalization/migration tests passed.
- New regression coverage checks detection after unrelated global-regex-cache churn:
  9,120 allocated bytes across 20 warm iterations, below a 128 KiB regression budget.
- Culture-switch checks cover en-US, tr-TR, and fr-FR.
- Full native suite: 1,895 passed, 11 filesystem-path tests failed. All 11 failures
  reproduced on the unoptimized comparison checkout.
- ARM64 Docker runtime tests used emulation on an Intel Mac, not native ARM hardware.
  The corrected broad run passed 505/506 tests after excluding one native-speed
  timing gate; an inconsistent BSB parser failure also reproduced without this change.
- Combined import/optimization validation passed 546/548 initially; the two
  timing-related failing fixtures passed all 10 cases when rerun in isolation.
- These results are not an all-green full suite or deployment approval. See the
  [validation record](RssValidation.md) for failures, exclusions, and reproduction.
- Live clients, a Linux source build, and native ARM execution remain unverified.

Run the new focused tests with:

```sh
dotnet test tests/Sportarr.Api.Tests/Sportarr.Api.Tests.csproj -c Release -p:SkipFrontendBuild=true --filter FullyQualifiedName~MotorsportRegexReuseTests --logger 'console;verbosity=detailed'
```

Use the unchanged full-benchmark command in the baseline document for comparisons.