# CPU Investigation: Developer Review Handoff

Investigation and initial benchmark date: 2026-09-06. This is the review entry point
for observed issues, implemented changes, evidence, validation gaps, and proposed
enhancements. It covers this investigation, not an exhaustive codebase audit.

## Review and Merge Order

1. [PR #1: preserve rejected import warnings](https://github.com/jasjeetsuri/Sportarr/pull/1).
   One sport-independent correctness fix, with regression tests. Commit `ba3cbb063`.
2. [PR #2: RSS benchmark and motorsport optimization](https://github.com/jasjeetsuri/Sportarr/pull/2).
   Review benchmark commit `2de6ab447` first, then optimization `28f58e008`, then
   documentation. The benchmark is evidence for the fix, not a separate prerequisite
   deployment. This PR does not contain or require the import fix.

Both target the personal fork's `main`, not upstream. They remain drafts for review,
not a claim that every validation gate passed. They can merge independently. A
disposable combined checkout was tested; neither PR was merged or deployed.

## Findings at a Glance

| Issue | Evidence | Scope | Resolution |
| --- | --- | --- | --- |
| Rejected imports retried every poll | 424 logged attempts/ffprobe augmentations/rejections for four files | Any sport | PR #1 |
| Periodic expensive RSS matching | Approximately 128 seconds for 900 releases x 71 events on the VPS | Observed F1 library | Reproducible benchmark in PR #2 |
| Fixed session regex cache thrashing | Local CPU/allocation stacks; 80.8% CPU samples and 92.7% sampled allocation bytes attributed to three detector methods | Motorsport session validation, not all sports | Regex reuse in PR #2 |
| Remaining matching allocations | Post-change profile: sport detection, normalization, location checks | Scope varies by path; only F1-candidate workload measured | Deferred optimization candidates |
| Filesystem test failures on macOS | 11 failures reproduced without optimization | Path/symlink helpers; root cause not assessed | Separate investigation, not modified |
| Timing-sensitive parser/policy tests | Failures under ARM emulation and combined native load; isolated outcomes differ | Existing timeout/timing behavior | Native ARM and timing-isolation follow-up |
| No requested event-age acquisition policy | Existing RSS age filters release publication, not event age | All unattended acquisition routes | Proposed enhancement, not implemented |

## Production Observations

Read-only investigation of Sportarr 4.1.6.1116, .NET 8.0.30, Docker, four logical
CPUs and approximately 23.42 GiB RAM, with no container CPU/memory limit. Library:
Formula 1, only 2026 monitored; SABnzbd-compatible NZBdav integration and rclone-backed
media access. No searches, imports, or RSS passes were triggered by the investigation.
Credentials, host addresses, raw private feeds, and media paths are omitted.

There were 1,746 stored events but only 121 monitored 2026 events. At the sampled
RSS pass, 71 aired candidates comprised 8 missing events and 63 events with files.
Historical unmonitored events did not cause this candidate count. RSS ran every
15 minutes across six feeds, fetching 1,089 releases and retaining 900 after age
filtering. Existing global backlog settings did not apply because both league
backlog opt-in flags were disabled.

At approximately 18:37:51.889 UTC matching began; it finished at 18:40:00.160 with
zero downloads/upgrades. CPU peaked at 207.7% (approximately 2.08 logical cores),
then returned to 1-2%. Earlier matching passes were approximately 122-127 seconds.
The idle sample averaged 1.27%. CPU percentages use one core as 100%.

The deployed binary was not proven identical to the source export. The fresh fork
was based on upstream `3cfbf7077`, and the relevant code paths were rechecked there.
No managed production stacks were captured; the method-level attribution below is
from the local synthetic workload, not the VPS.

## Fix 1: Stable Import Rejection

Between approximately 17:27:33 and 18:24:56 UTC, four Italian GP session downloads
produced 424 import attempts, 424 ffprobe augmentations, and 424 non-upgrade rejections.
Existing and candidate WEBDL-1080p quality scores were both 590. The client entries
were removed before the investigator connected; that was not an investigation action.

The [monitor](../../src/Services/EnhancedDownloadMonitorService.cs) polls warning rows
and previously replaced `ImportWarning` with client `Completed`. The
[importer](../../src/Services/FileImportService.cs) inspected media, rejected the
non-upgrade, saved `ImportWarning`, and returned null without consuming an exception
retry. Thus the same job was imported again on the next poll.

PR #1 retains the warning after refreshing client metadata for non-failure responses.
Missing-client and failure handling remain on their existing paths. It neither deletes
files nor marks a rejected file imported nor blocklists it. The existing force-import
endpoint calls the importer directly; pending/extraction retry behavior is unchanged.
There is no newly added automatic retry on policy or library changes.

The regression failed before the fix and passed afterward. Fake SAB responses and
an in-memory database exercise repeated polls, reloads/new monitor instances,
nonterminal client responses, unmonitored events, metadata, and missing tracking.
This is not a full restart/process or real ffprobe test. Historical CPU savings from
this fix were not isolated, and ffprobe is a separate process.

## Fix 2: Reuse Motorsport Regexes

The [RSS selector](../../src/Services/RssSyncService.cs) already parses each release
once and uses existing caches. Profiling found a different bottleneck: the three
session methods in [EventPartDetector](../../src/Sportarr.Data/Services/EventPartDetector.cs)
repeatedly use more fixed regex patterns than the framework's 15-entry static cache
holds. Regex construction dominated the measured workload.

A private concurrent cache now retains regexes by pattern, options, and culture.
Pattern text, precedence, matching rules, and global regex-cache settings are unchanged.
It retains internal patterns, not arbitrary release titles or matching decisions.
Motorsport event validation invokes these methods; football, basketball, combat, and
other non-motorsport candidates skip that section. Do not generalize the F1 speedup
to every sport or workload.

| Untraced full pass | Before | After |
| --- | ---: | ---: |
| First wall time | 62.451 s | 12.171 s |
| Repeat wall time | 57.991 s | 9.741 s |
| First thread allocated bytes | 28,332,635,688 | 2,067,482,920 |
| Repeat thread allocated bytes | 28,279,910,752 | 2,025,720,032 |
| Expected matches on each pass | 270 | 270 |

Same 900-release/71-event synthetic F1-candidate corpus; every expected event ID is
asserted, including nonmatches. Approximately 5-6x faster and 93% fewer cumulative
allocations in these local runs. Allocated bytes are not retained heap or peak RAM.
Individual runs, not confidence intervals; no native ARM performance claim.

See [baseline reproduction](../../tests/RssMatchingBenchmark.md),
[profiling and optimization details](../../tests/RssMatchingOptimization.md), and
[complete validation record](../../tests/RssValidation.md). The benchmark excludes
HTTP, database candidate selection, upgrade decisions, and real downloads; it is
not a production feed replay or full RSS integration benchmark.

## Validation and Review Gates

507 optimization-focused native tests passed. The full native suite passed 1,895
and failed 11 filesystem tests; all 11 reproduced without optimization. The direct
import ARM64-emulation set passed 10/10. Broad ARM64 validation was not all green:
source-fixture mounting was corrected, one native-speed timing gate was explicitly
excluded, and inconsistent BSB parser failures reproduced without the optimization.
Combined native validation passed 546/548; both timing-related failing fixtures then
passed 10/10 cases in isolation. No timeouts or assertions were relaxed to make a pass.

Before production: obtain native Linux ARM validation, investigate outstanding test
failures, run an application-image smoke test with disposable config/media, review
manual import/retry and transient-path behavior, and agree on backup/rollback and
deployment authorization. A follow-up observed production RSS pass must establish
actual savings. Test success does not authorize production changes.

## Proposed Enhancements, Not Implemented

- Per-league automatic missing acquisition and upgrade controls, independently enabled,
  with separate event-age limits. Defaults preserve current behavior; zero means unlimited.
- Apply-once editable presets: Recent Events (14/14 days), Archive (unlimited missing,
  14-day upgrades), Manual Only, Custom. They must not silently alter monitoring,
  source selection, quality profiles, or existing retention settings.
- Use UTC event start time; exactly the cutoff remains eligible. Keep release publication
  age separate. Preserve existing files and allow explicit manual searches. Existing
  `EventRetentionService` deletes/recycles files and unmonitors events, so it is not the
  owner of a non-destructive acquisition policy.
- Enforce new policy across unattended RSS/pushed releases, backlog, queued automatic
  searches, delayed-release expiry, automatic regrabs, DVR, and catchup. Filter early and
  recheck before a new transfer. Do not prevent import of already accepted downloads.
- Evaluate missing/upgrade per required part; season packs are atomic. Unknown/mixed
  coverage needs conservative handling rather than claiming selective transfer.
- Add decision previews, validation, backward-compatible SQLite/PostgreSQL migrations,
  API/bulk handling, frontend tests, and fixed-clock cutoff/manual-bypass tests before
  exposing nondefault policy values.
- Add compact pass diagnostics: candidate/release/pair counts, timing by phase,
  outcomes, skipped reasons, cache metrics, and suppressed import retries. Avoid
  per-pair production logging and label process-wide CPU/allocation counters accurately.
- Profile remaining normalization/location/sport-detection costs separately. Preserve
  country/demonym aliases and indexer-specific eligibility; do not reintroduce literal
  title-word prefilters or deduplicate distinct indexer decisions just by title.
- A later low-impact processing mode must use measured pacing with actual idle time,
  cancellation, and verified non-overlap. It must not silently truncate feed coverage
  or promise a hard CPU quota.

Longer RSS intervals reduce frequency, not peak work; smaller feed limits can miss
releases. Slower polling masks the import loop rather than fixing it. These operational
tradeoffs should not substitute for the correctness and measured performance changes.