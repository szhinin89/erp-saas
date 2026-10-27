# ADR-027 — Arquitectura unificada de errores

Estado: Accepted. Decisión vigente; estado del seguimiento de gobernanza en
[GOV-002](../../STATUS.md#gov-002--closed). No certifica adopción exhaustiva por todos los módulos.

## Decisión y justificación

Un contrato de fallo atraviesa las capas: Domain expresa reglas sin HTTP;
Application transporta códigos del catálogo; los adaptadores API deciden HTTP;
el frontend normaliza una vez y presenta el error por su contexto.
Evita mapeos por texto, parsers por pantalla y excepciones técnicas expuestas al usuario.

## Responsabilidad y límites

No redefine mensajes visuales, permisos, datos fiscales ni reglas de cada módulo.
No conserva una categoría HTTP alternativa ni una receta de migración como contrato actual.
La auditoría de julio no es evidencia del código de hoy.

## SSOT y dependencias

Contrato: [error-handling.md](../architecture/error-handling.md).
Presentación: [visual-messages.md](../architecture/visual-messages.md) y
[form-validation.md](../architecture/form-validation.md).
Deudas conocidas de adopción: [error-handling.md](../architecture/error-handling.md#deudas-conocidas-y-límite-del-cierre).
