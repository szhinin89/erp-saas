# Configuración de secuencias documentales SRI

Decisión conservada por referencias existentes; complementa ADR-019 sin crear otro contador.

## Decisión y justificación

El número inicial configura la misma DocumentSequence, por Company + EmissionPoint +
DocTypeCode. Branch es contexto de acceso, no otra serie fiscal. El ambiente queda en
el documento/Access Key; no se añade Environment a la clave sin necesidad normativa demostrada.
HasBeenUsed distingue captura real de un número inicial configurado: CurrentSeq > 1
no demuestra uso. Los documentos conservan número y trazabilidad de emisión.

## Responsabilidad y límites

No se captura en Draft ni se reutiliza un número consumido por un documento anulado.
Configurar la serie no sustituye CaptureNextAsync ni permite escribir el contador desde
handlers. El comando actual rechaza ajustar tras uso real; un ajuste restringido con permiso
especial, motivo y auditoría es una ampliación futura, no una capacidad existente. La auditoría granular futura no se presenta como tabla existente.

## SSOT y dependencias

[Secuencias Documentales](../architecture/frozen-infrastructure.md#secuencias-documentales).
Reglas globales: [CLAUDE](../../CLAUDE.md). Captura: [ADR-019](ADR-019-document-sequence-infrastructure.md).
Consumidores conocidos: controller de secuencias y su snapshot OpenAPI conservan esta ruta.
Estado y gates: [STATUS](../../STATUS.md).
