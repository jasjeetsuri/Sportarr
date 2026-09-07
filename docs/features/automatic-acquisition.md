# Automatic Acquisition Policy

Each league has independent automatic missing-download and upgrade settings in
the add/edit league dialog. Both default to enabled with unlimited age windows,
preserving existing behavior. A policy permits work; it does not enable indexers,
backlog searches, DVR, monitoring, or upgrades forbidden by a quality profile.

| Setting | Default | Meaning |
| --- | --- | --- |
| Automatic missing downloads | Enabled | Allow unattended acquisition of missing events or parts |
| Automatic upgrades | Enabled | Allow unattended replacement of an existing event or part file |
| Missing event age | 0 | Maximum event age in days; 0 is unlimited |
| Upgrade event age | 0 | Separate maximum event age; 0 is unlimited |

Age is measured from the event's UTC start time, not release publication or file
import time. Exactly at the cutoff remains eligible. Future events pass this
policy so DVR can plan ahead; existing timing rules still apply. Ages must be
non-negative 32-bit integers. The two windows are independent.

## Presets

Presets copy values into the current editor. There is no ongoing link or global
inheritance. Values remain editable; changing them displays Custom unless they
match another preset.

| Preset | Missing | Upgrades |
| --- | --- | --- |
| Unlimited | Enabled, unlimited | Enabled, unlimited |
| Recent 14 Days | Enabled, 14 days | Enabled, 14 days |
| Archive | Enabled, unlimited | Enabled, 14 days |
| Manual Only | Disabled | Disabled |

For a 30-day policy, apply Recent 14 Days and change both ages to 30.
Archive does not request older seasons or bypass global backlog limits.

## Scope and Safety

The policy applies to RSS/pushed releases, automatic searches (including queued
searches, backlog and background regrabs), delayed-release expiry, and newly
automatically scheduled DVR/catchup work. Current settings and file ownership
are rechecked before starting a transfer. Refused holds and scheduled automatic
recordings are cancelled with a reason, not blocklisted as bad media.

Explicit manual event searches, grabs, and manually scheduled recordings bypass
this policy only. Other quality, client, path and safety checks remain. Existing
batch-search APIs that already mark work automatic continue to do so.
Applying policy values does not change running transfers, files, monitoring,
season selection, or import behavior.

**Retention is separate and can delete files.** Presets do not change Retention
Days. Disable destructive retention separately when preserving files is required.

Missing versus upgrade is checked per detected/requested part; a full-event file
covers every part. Packs cannot prove all contained events are eligible, so any
restrictive policy makes automatic packs unavailable. Manual selection remains
possible. Old held releases lack pack provenance and are treated as unverified
packs under restrictive policies; new holds persist it.

Existing scheduled recordings have unknown origin and migrate as manual-compatible
(`IsAutomatic = false`), without retroactive cancellation. Newly scheduled automatic
recordings preserve their origin through fallback-channel rotation.

## API and Migration

Create/edit payloads expose `automaticMissingEnabled`, `automaticUpgradesEnabled`,
`automaticMissingMaxAgeDays`, and `automaticUpgradeMaxAgeDays`. Single and bulk
edits leave omitted policy fields unchanged; create defaults to enabled/unlimited.
Incorrectly typed, negative, fractional, or overflowing values are rejected.
No impact preview or bulk-policy UI is included.

SQLite and PostgreSQL migrations add four league fields, DVR origin, and nullable
pack provenance. SQLite ensures the historical pending table exists before adding
its column: the application historically creates that table in startup repair
after migrations. No historical migration is newly made discoverable.

Back up the database/config before upgrading. Rollback requires restoring that
backup with the matching previous app; downgrade is unverified. See the
[validation evidence](../../tests/AutomaticAcquisitionPolicy.md) before deployment.