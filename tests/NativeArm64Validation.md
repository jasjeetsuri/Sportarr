# Native ARM64 validation

The `Native ARM64 Validation` workflow runs on GitHub's `ubuntu-24.04-arm`
hosted runners on pull requests to `main`, or through `workflow_dispatch`
once the workflow is on the default branch. It does not publish or deploy.

## Checks

- `tests`: source-build the Release backend and run the entire test suite,
  without exclusions or weakened assertions. Upload TRX results even on failure.
- If the full suite passes, run the synthetic 900-release by 71-event RSS
  benchmark in a fresh test process and preserve its measurements in TRX.
- `image-smoke`: independently build the frontend and framework-dependent
  ARM64 publish output, then build the repository's actual Dockerfile.
- Assert host, Docker engine, and image architecture. No QEMU setup is used.
- Start the normal image entrypoint with a disposable `/config` volume, no
  published ports, no external network, no host data, and a 2 GiB memory limit.
- Check `/ping`, the served React root and a JavaScript asset, application UID,
  SQLite integrity and migration history, and persistence across container restart.
- Remove the container and its anonymous volume on exit, including failures.

The Dockerfile requires both architecture input directories; the unused x64
directory is deliberately empty. ARM64 is explicitly selected for the build.
The production Dockerfile and application code are unchanged.

## Reproduce

On a native Linux ARM64 Docker host with .NET 8 and Node.js 20:

```bash
dotnet test tests/Sportarr.Api.Tests/Sportarr.Api.Tests.csproj -c Release -p:SkipFrontendBuild=true
npm --prefix frontend ci
npm --prefix frontend run build
cp -R _output/UI/. src/wwwroot/
dotnet publish src/Sportarr.csproj -c Release -r linux-arm64 --self-contained false -p:PublishSingleFile=false -p:SkipFrontendBuild=true -o publish/docker-linux-arm64
mkdir -p publish/docker-linux-x64
docker build --platform linux/arm64 --tag sportarr-validation:arm64 .
bash .github/scripts/smoke-container.sh sportarr-validation:arm64
```

Local configuration checks:

```bash
bash -n .github/scripts/smoke-container.sh
docker run --rm --network none -v "$PWD:/repo:ro" -w /repo rhysd/actionlint:1.7.7 .github/workflows/native-arm64-validation.yml
```

## Evidence and limits

Fork PR: <https://github.com/jasjeetsuri/Sportarr/pull/3>.
Initial hosted run: <https://github.com/jasjeetsuri/Sportarr/actions/runs/34064198340>.
On September 6, 2026, both native ARM64 jobs passed at commit `c64029e28`:

- Full source-built backend suite: 1,912 passed, 0 failed, 0 skipped.
- Frontend build, ARM64 publish, production image build, startup, UI asset,
  SQLite integrity/migrations, and restart persistence: passed.
- Full synthetic RSS workload: 900 releases by 71 events per pass; 270 matches
  each, with every selected event ID asserted.

| Pass | Elapsed | Process CPU | Thread allocations |
| --- | ---: | ---: | ---: |
| First | 8.5307 s | 9.3500 s | 2,064,159,872 bytes |
| Repeat | 6.1764 s | 6.2000 s | 2,022,656,264 bytes |

These are optimized native ARM64 measurements, not a same-host before/after
comparison or a VPS performance guarantee. Allocations are cumulative, not
retained memory. Build warnings remain; the workflow does not promote them to
errors. The subsequent smoke-script revision checks PID 1's UID directly and
writes the persistence marker as that UID; see the PR checks for that revision.

Earlier import and RSS optimization PRs are merged into the personal fork's
`main`, not upstream. The earlier emulation results in [RssValidation.md](RssValidation.md)
remain historical evidence, not native Linux results.

The smoke check is HTTP-level packaging/startup validation, not a browser test.
It does not test PostgreSQL, existing-database upgrades, live download clients,
GPU passthrough, external metadata services, or deployment on the VPS.
No application logs or generated config are uploaded because startup can include
sensitive configuration. Test artifacts contain synthetic test results only.