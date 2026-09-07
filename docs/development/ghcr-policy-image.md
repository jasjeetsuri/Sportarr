# Fork Policy Image on GHCR

The fork-only `Publish Policy to GHCR` workflow runs on pushes to
`feat/automatic-acquisition-policy`. It does not publish pull-request code or
upstream releases. Native AMD64 and ARM64 jobs run the full backend suite, policy
component tests, frontend build, and image startup/restart checks before pushing.
Only when both pass are multi-platform tags published:

- `ghcr.io/jasjeetsuri/sportarr:acquisition-policy` follows successful branch builds.
- `ghcr.io/jasjeetsuri/sportarr:sha-<full-commit-sha>` identifies a source revision.

The workflow uses the repository's built-in `GITHUB_TOKEN`, with package-write
permission only on publishing jobs. No Docker Hub credentials are needed. Image
source/revision labels identify the fork. It never updates `latest`, deploys a
service, or merges a PR. Publication does not make the draft production-ready;
see [remaining validation gaps](../../tests/AutomaticAcquisitionPolicy.md).

## Compose

After a successful publishing run, change only your existing service's image:

```yaml
services:
  sportarr:
    image: ghcr.io/jasjeetsuri/sportarr:acquisition-policy
```

Keep existing ports, volume mappings, environment, user IDs, and device settings.
Compose chooses AMD64 or ARM64 automatically; no `platform` override is needed.
For a controlled deployment, use the published `sha-...` tag or immutable registry
digest instead of the moving branch tag.

GHCR creates new packages private by default. To allow anonymous pulls, the
package owner can change **Package settings > Change visibility > Public** at
`https://github.com/users/jasjeetsuri/packages/container/sportarr/settings`.
Making a package public is a separate explicit owner decision. Otherwise, log in
on the Docker host with an account authorized to read the package:

```sh
docker login ghcr.io -u jasjeetsuri
```

At the password prompt, enter a GitHub personal access token (classic) with
`read:packages`; do not use the account password or paste a token into chat.

Back up the database/config before upgrading. For SQLite, stop Sportarr before
copying its entire mounted `/config` directory, including any WAL files. For
PostgreSQL, take a database backup as well as the configuration backup. Preserve
the previous image reference. Then run from the directory containing your Compose
file:

```sh
docker compose pull sportarr
docker compose up -d --no-deps sportarr
docker compose logs --tail=100 sportarr
```

Do not use `docker compose down -v`. Rolling back across database migrations
requires the matching pre-upgrade database/config backup and previous image;
changing only the image tag is not a validated downgrade.