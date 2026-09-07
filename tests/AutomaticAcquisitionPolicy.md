# Automatic Acquisition Policy Validation

Status: draft implementation, not deployed or ready for production promotion.
See the [feature contract](../docs/features/automatic-acquisition.md).

## Review Order

1. `src/Helpers/AutomaticAcquisitionPolicy.cs`: part-aware evaluator and fresh guard.
2. Search, RSS and pending reaper call sites: early filtering and late checks.
3. DVR/catchup origin propagation and start checks.
4. League API/model fields and existing editor integration.
5. Both provider migrations/designers/snapshots and policy tests.

This is an all-path draft, not a small production patch. Generated EF designers
contain whole-model snapshots and dominate the diff. Review them separately from
handwritten behavior. No unrelated test failures or timing thresholds were changed.

## Local Evidence

macOS x64; .NET SDK 8.0.424/runtime 8.0.30; Node 22.14.0; lockfile dependencies.
Synthetic data only, with no production database, media, clients, or VPS changes.

- Policy fixture: 20 passed. Defaults, configurable independent windows, cutoff
  and adjacent tick, future dates, manual bypass, changed settings across contexts,
  parts/full files/packs, actual RSS refusal, catchup cancellation versus manual
  validation, and real reaper cancellation with mixed single/legacy/pack candidates.
- Final policy plus neighboring DVR run: 37 passed, 0 failed.
- SQLite migration SQL passed with both existing and absent pending tables.
  Real disposable startup initially failed on the missing historical table, then
  succeeded after the migration fix: `/ping` 200 and new migration in history.
- Both EF provider `has-pending-model-changes` checks passed after all fields.
  Default assertions cover both migrations.
- Policy component: 3 passed. Frontend typecheck/build and focused new-file ESLint
  passed. Full frontend results below are not all green.
- Browser/API: persisted 45/7 and explicit false; rejected negative, fractional,
  overflow and string-boolean inputs with 400. Retention and monitoring unchanged.
  Editor loaded values, applied 14/14, changed missing to 30, saved and reloaded
  as Custom 30/14. Control bounds and screenshots checked at 1280x900 and 390x844.
- Full backend before the last two reaper tests: 1,918 passed, 12 failed, 0 skipped.
  Eleven are the macOS filesystem/path failures previously reproduced on baseline.
  Existing normalization timing assertion also failed at 54s against its 30s limit.
  Earlier isolated reruns passed; no threshold was changed.
- Full frontend: 52 passed, 7 failed in unchanged `useSettings` and `AddEventModal`
  fixtures. These fixtures also failed on pre-policy commit `9904a7510`: 11 passed,
  10 failed, including extra timeouts. Baseline failures, not identical counts.
- Existing Node engine mismatch and npm audit warnings remain; no dependency
  upgrades were included.

## Reproduction

```sh
dotnet test tests/Sportarr.Api.Tests/Sportarr.Api.Tests.csproj \
  -c Release -p:SkipFrontendBuild=true \
  --filter FullyQualifiedName~AutomaticAcquisitionPolicyTests
dotnet test tests/Sportarr.Api.Tests/Sportarr.Api.Tests.csproj \
  -c Release -p:SkipFrontendBuild=true
npm --prefix frontend ci
npm --prefix frontend test -- --run src/components/__tests__/AcquisitionPolicyFields.test.tsx
npm --prefix frontend run build
```

## Promotion Gaps

Native ARM64 run [34068796663](https://github.com/jasjeetsuri/Sportarr/actions/runs/34068796663)
passed on code commit `0660fe047`: **1,932 passed, 0 failed, 0 skipped**.
The independent image job passed frontend build, ARM64 publish, startup, UI
assets, SQLite integrity, non-root process and restart persistence checks.
Synthetic default-policy RSS workload (900 releases x 71 events): first 8.3778s,
CPU 9.1700s, thread allocations 2,063,729,528 bytes; repeat 6.3287s, CPU 6.3600s,
allocations 2,022,656,256 bytes. Both passes matched 270 expected events.
These are cumulative allocations, not retained memory or a production guarantee.

- PostgreSQL execution/upgrade is unverified: Docker Desktop stopped responding
  to container creation and `docker ps`. Model checks do not replace real DB tests.
- Live client, FFmpeg and provider integration/soak tests have not run. Refusal
  fixtures do not prove every eligible/manual success path or concurrent change.
  Transfer checks are not atomic with external I/O.
- Historical user-database upgrades, rollback restoration and the bulk API
  atomicity/invalid-payload matrix remain untested end to end.
- Legacy recording origin is intentionally unknown; unverified packs are
  conservatively manual-only under restrictive settings.
- No new CPU benchmark claim: early filters reduce some candidates, while fresh
  database checks have a cost. Preview, bulk UI, pacing and rollout are later work.