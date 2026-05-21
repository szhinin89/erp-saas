# Refresh token rotation — security model

## Resumen

| Aspecto | Implementación |
|---------|----------------|
| Almacenamiento refresh | Cookie httpOnly, `Path=/api` |
| Access token | Solo memoria (SPA) |
| Rotación | Atómica (transacción EF) |
| Familia | `FamilyId` + `RotationDepth` |
| Replay benigno | Grace `Auth:RefreshRotationGraceSeconds` |
| Replay sospechoso | `RevokeFamilyAsync` |
| Multi-tab FE | Web Locks + BroadcastChannel |
| Rate limit | IP + user + family |

## Threat model (resumen)

| Amenaza | Mitigación |
|---------|------------|
| Robo refresh token | Rotación + detección reuso |
| Replay concurrente | ExecuteUpdate / transacción + Web Locks |
| Logout falsos multi-tab | BC sync + retry bootstrap |
| Refresh storm | Rate limiting |
| XSS robo access | Token corto en memoria; refresh no accesible JS |

## Referencias

- [`AUTH_RULES.md`](../../AUTH_RULES.md)
- [`docs/decisions/ADR-003-refresh-token-rotation.md`](../../docs/decisions/ADR-003-refresh-token-rotation.md)
- Código: `RefreshTokenService`, `authRefreshManager.ts`
