# Platform Control Plane — CI Guard Report

**Generated:** 2026-08-28T16:10:48.995Z
**Status:** PASS
**Violations:** 0

## Summary

| Check | Result | Violations |
|-------|--------|------------|
| static-forbidden-patterns | PASS | 0 |
| platform-imports | PASS | 0 |
| frontend-routes | PASS | 0 |
| api-endpoints | PASS | 0 |
| subscriber-api-surface | PASS | 0 |

## Violations

_No violations detected._
## Endpoints detected (frontend)

```json
{
  "total": 110,
  "allowed": 110,
  "legacyViolations": 0,
  "allowlistViolations": 0
}
```

## Design

- Preventive, mandatory, fail-fast guard for Platform Control Plane.
- Legacy SuperAdmin API surface must not reappear.
- Config: `tools/ci/platform-guard-config.json`

