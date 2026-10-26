# ADR-025 — RIDE como presentación fiscal desacoplada

Estado: Accepted — límites de diseño conservados. No significa preimplementación global de RIDE.

## Decisión y justificación

Separar XML fiscal, modelo neutro, parser, plantilla, branding, renderer y storage
permite añadir tipos de comprobante sin modificar el pipeline fiscal ni depender de
QuestPDF desde Application. El archivo físico y la metadata tienen responsabilidades
separadas. La caché versionada evita reutilizar un PDF con interpretación/branding obsoletos.

## Responsabilidad y límites

RIDE no emite, firma ni cambia XML autorizado. La extensión background usa el mismo
pipeline con contexto explícito, preservando autorización HTTP y aislamiento; no es
un segundo motor. La metadata de autorización se compone fuera del parser XML.
No se mantienen como vigentes los inventarios antiguos de tipos sin parser/plantilla.
Resolución jerárquica avanzada y otros diseños pendientes requieren fase propia.

## SSOT y dependencias

[Backend — RIDE](../architecture/backend.md#ride), IFileStorage y lectura interna de
ElectronicDocuments. Orquestación de adjuntos: [Communications](../communications/COMMUNICATIONS-ARCHITECTURE.md).
Capacidades y gates: [FEATURES](../../FEATURES.md), [STATUS](../../STATUS.md).
