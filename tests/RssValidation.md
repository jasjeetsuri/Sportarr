# CPU Fix Validation Record

## Revisions and Environment

- Upstream baseline: `3cfbf7077` on `main`.
- Import fix: `ba3cbb063`, independent of RSS work.
- Benchmark: `2de6ab447`; optimization: `28f58e008`.
- Combined disposable worktree: import fix plus cherry-picked benchmark and optimization.
- Native host: macOS x64, SDK 8.0.424, runtime 8.0.30, Release builds.
- Docker Desktop Linux x86_64 engine; ARM64 execution is emulated, limited to two CPUs
  and 3 GiB. Containers had no network, a read-only root/repository, and temporary
  writable storage. No production configuration, media, or credentials were mounted.
- SDK container: `mcr.microsoft.com/dotnet/sdk:8.0`, reported SDK 8.0.424/runtime
  8.0.30, Debian 12, `linux-arm64`. Pulled digest:
  `sha256:bb32ba3ba3ea36e38572d9d8db76fa15f7cbf722f3f886e06bca6d528bd4fba8`.
- Containers ran the portable test assemblies built on macOS. This is runtime
  compatibility coverage, not a Linux source build, native ARM benchmark, application
  image smoke test, or deployment validation.

## Results, Including Failures

| Check | Result |
| --- | --- |
| Initial import-focused native set | 44/44 passed |
| Initial benchmark-related native set | 28/28 passed |
| Optimization matching-related native set | 507/507 passed |
| Full 900 x 71 untraced benchmark, baseline and optimized | All expected IDs passed on both passes; 270 matches each |
| Optimization full native suite | 1,895 passed, 11 failed, 1,906 total |
| Unoptimized filesystem comparison | Same 11 failures reproduced; 14 passed, 25 total |
| Unoptimized import plus BSB native set | 56/56 passed |
| Initial optimization ARM64 set | 503 passed, 4 failed, 507 total |
| Corrected optimization ARM64 set | 505 passed, 1 failed, 506 selected |
| Isolated optimized BSB ARM64 fixture | 11 passed, 1 failed |
| Unoptimized import plus BSB ARM64 set | 54 passed, 2 failed, 56 total |
| Direct import warning/terminal-state ARM64 set | 10/10 passed |
| Combined fixes native set | 546 passed, 2 failed, 548 total |
| Isolated rerun of combined failing fixtures | 10/10 passed |

The full native suite's 11 failures are in `PathResolutionTests` (1),
`RootFolderValidatorTests` (8), and `RootFolderSymlinkTests` (2). They concern `/etc`
and symlink/path resolution on macOS. The identical failures on the unoptimized
checkout establish that they are not introduced by the motorsport change. Their
root causes and any security implications have not been investigated here.

The first ARM64 run mounted only assemblies, so three migration tests could not
find repository source. Mounting the repository read-only resolved those failures.
The fourth failure was the existing normalization test's 30-second wall-clock gate,
which took approximately 266 seconds under emulation. Only that timing test was
excluded from the corrected 506-test run; its assertion was not changed.

The corrected run failed `BritishSuperbikeTests.Parse_BsbRelease_ExtractsOrganisationRoundAndSession`.
An isolated rerun instead failed the Race Two case of `Parse_NumberedRaces_AreToldApart`.
That Race Two failure also occurred on the unoptimized checkout under emulation.
The parser skips regex patterns exceeding its existing 250 ms timeout; timing is a
plausible explanation, not a captured proof for each BSB failure. The unoptimized
ARM64 run also threw an explicit `RegexMatchTimeoutException` in
`DownloadFailurePolicyTests.IsPathNotReadyError_TreatsMissingPathsAsAWait`.

Combined native validation had a failure-policy path regex timeout and a 30.932-second
normalization pass exceeding the same 30-second gate. Both fixtures passed in isolation
(10 cases; normalization approximately 14 seconds). A retry pass does not erase the
initial failures. Native ARM CI and repeatable timing isolation remain review gates.

## Reproduce

Build and run the full native suite from the repository root:

```sh
dotnet test tests/Sportarr.Api.Tests/Sportarr.Api.Tests.csproj -c Release -p:SkipFrontendBuild=true --logger 'console;verbosity=minimal'
```

The optimization's broad filter was:

```text
FullyQualifiedName~Matching|FullyQualifiedName~Session|FullyQualifiedName~EventPart|FullyQualifiedName~Parser|FullyQualifiedName~Rss|FullyQualifiedName~Normalization|FullyQualifiedName~MigrationDiscovery|FullyQualifiedName~MotorsportRegexReuse
```

The import-focused filter was:

```text
FullyQualifiedName~DownloadMonitorImportWarningTests|FullyQualifiedName~FileImportServiceTerminalStateTests|FullyQualifiedName~DownloadFailurePolicy|FullyQualifiedName~QueueRemovalValidationTests|FullyQualifiedName~ExistingFileUpgradeGateTests|FullyQualifiedName~MigrationDiscoveryTests
```

The combined native run used the union of those filters. The corrected ARM64 run
used the optimization filter wrapped in parentheses, followed by:

```text
&FullyQualifiedName!~IsReleaseMatch_LargeRssStyleMatchingPass_CompletesQuickly
```

Set `FILTER` to the desired expression and execute the already-built assemblies:

```sh
docker run --rm --platform linux/arm64 --cpus 2 --memory 3g \
  --network none --read-only --tmpfs /tmp:rw,exec,size=1g \
  -e DOTNET_CLI_HOME=/tmp -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -v "$PWD:/repo:ro" -w /repo mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet vstest /repo/tests/Sportarr.Api.Tests/bin/Release/net8.0/Sportarr.Api.Tests.dll \
  "--TestCaseFilter:$FILTER" '--logger:console;verbosity=minimal' \
  --ResultsDirectory:/tmp/test-results
```

Use the [benchmark instructions](RssMatchingBenchmark.md) for performance comparisons.
Do not compare emulated ARM timing with native macOS timing or infer VPS CPU savings
from these tests. Both PRs remain drafts; no merge or production deployment was performed.