# ADR-027 — Arquitectura unificada de errores

Estado: Accepted. Decisión vigente; la adopción se sigue en GOV-002, sin asumir
que las fases descritas antes de las correcciones siguen sin iniciar.

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
Adopción pendiente: [GOV-002](../agent/WORK_QUEUE.md#gov-002--adopción-del-contrato-de-errores).
