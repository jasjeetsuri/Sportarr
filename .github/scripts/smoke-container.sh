#!/usr/bin/env bash
set -euo pipefail

image=${1:?Usage: smoke-container.sh IMAGE}
container="sportarr-smoke-${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-1}-$$"

cleanup() {
    if [ "$?" -ne 0 ]; then
        docker inspect --format 'Running={{.State.Running}} ExitCode={{.State.ExitCode}} Error={{.State.Error}}' "$container" || true
    fi
    docker rm -fv "$container" >/dev/null 2>&1 || true
}
trap cleanup EXIT

wait_ready() {
    docker exec "$container" curl --fail --silent --show-error \
        --retry 60 --retry-connrefused --retry-delay 2 --retry-max-time 180 \
        --max-time 5 http://localhost:1867/ping
}

test "$(docker image inspect --format '{{.Architecture}}' "$image")" = arm64
docker run --detach --name "$container" --network none --memory 2g --cpus 2 \
    --volume /config --tmpfs /tmp:rw,size=256m \
    --env PUID=13001 --env PGID=13001 "$image" >/dev/null

wait_ready
test "$(docker exec "$container" awk '/^Uid:/ {print $2}' /proc/1/status)" = 13001
docker exec "$container" curl --fail --silent --max-time 10 http://localhost:1867/ \
    | grep -q '<div id="root"'
asset=$(docker exec "$container" find /app/wwwroot/assets -name '*.js' -print -quit)
test -n "$asset"
docker exec "$container" curl --fail --silent --max-time 10 \
    "http://localhost:1867/assets/${asset##*/}" >/dev/null
test "$(docker exec "$container" sqlite3 /config/sportarr.db 'PRAGMA integrity_check;')" = ok
test "$(docker exec "$container" sqlite3 /config/sportarr.db 'SELECT count(*) FROM __EFMigrationsHistory;')" -gt 0
docker exec --user 13001:13001 "$container" touch /config/smoke-persistence-check
docker restart "$container" >/dev/null
wait_ready
docker exec "$container" test -f /config/smoke-persistence-check
test "$(docker exec "$container" sqlite3 /config/sportarr.db 'PRAGMA integrity_check;')" = ok
printf '\nARM64 application startup, UI, SQLite, and restart smoke checks passed.\n'