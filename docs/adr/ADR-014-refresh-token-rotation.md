# ADR-014: Refresh token rotation enterprise

## Estado
Aceptado (2026-05)

## Contexto
SPA con cookie httpOnly, multi-tab, multi-dispositivo. Riesgo de replay y logout falsos.

## Decisión
- Rotación atómica (transacción EF)
- **FamilyId** por sesión/dispositivo
- Reuso benigno (grace configurable) vs sospechoso → `RevokeFamilyAsync`
- Frontend: Web Locks + BroadcastChannel + single refresh manager

## Consecuencias
- ✅ Otras sesiones legítimas no se invalidan en replay
- ✅ Coordinación cross-tab sin compartir refresh token
- ⚠️ Requiere migración `family_id` en `refresh_tokens`

## Referencias
- [`AI-RULES/SECURITY.md`](../../AI-RULES/SECURITY.md)
- [`docs/security/auth/refresh-rotation.md`](../security/auth/refresh-rotation.md)
