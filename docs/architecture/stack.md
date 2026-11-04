# Stack oficial — herramientas permitidas

**Detalle operativo y versiones:** [`docs/DEVELOPMENT.md#stack-oficial`](../DEVELOPMENT.md#stack-oficial)

**Verificación CI:** `scripts/ci/verify-stack-allowlist.ps1` + `scripts/stack-allowlist.json`

Cursor adapter: `.cursor/rules/stack-tools-source-of-truth.mdc`

---

## Reglas operativas

- **No** proponer, generar ni configurar herramientas fuera de la lista oficial sin autorización explícita del usuario.
- Si una necesidad no está cubierta, pedir confirmación y ofrecer alternativas **dentro** del stack listado.
- Antes de migraciones/scripts/config nueva: verificar herramienta en `docs/DEVELOPMENT.md`.
- Entorno oficial: Docker Compose (PostgreSQL + Redis), CI GitHub Actions, xUnit/Playwright.

---

## Referencias por responsabilidad

Lista y versiones: [DEVELOPMENT](../DEVELOPMENT.md#stack-oficial).
Migraciones: [DATABASE](../DATABASE.md#migraciones).
Frontera ERP ↔ Platform: [Arquitectura](architecture.md#reglas-de-arquitectura-que-no-se-rompen).

---

## Cambiar el stack

1. Actualizar `docs/DEVELOPMENT.md#stack-oficial`
2. Actualizar `scripts/stack-allowlist.json`
3. Documentar decisión (ADR si aplica)
4. Mantener la lista completa únicamente en `docs/DEVELOPMENT.md#stack-oficial`.
