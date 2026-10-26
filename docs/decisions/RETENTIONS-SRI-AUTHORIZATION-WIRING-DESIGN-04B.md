# Retenciones — integración XML con el emisor único

Decisión de integración conservada por referencias de IElectronicDocumentXmlSupplier;
no prescribe un wiring futuro ni otro pipeline fiscal.

## C. Contrato y motivo

IElectronicDocumentXmlSupplier entrega ElectronicDocumentXml por tipo a partir de
ElectronicDocumentSourceReference. Supplier evita confundir la abstracción con Provider
(datos comerciales), Builder (ensamblado XML) y Source (identidad del origen).

## E. Resolver

El resolver selecciona la implementación por DocumentType. Mantener esa selección
fuera del Issuer evita ramas por origen y un segundo emisor de retenciones.

## F. Camino comercial

El supplier comercial conserva providers/builders de Factura y Nota de crédito.

## G. Camino de Retención

El supplier de Retención reutiliza su generación de XML. La validación XSD,
firma y SOAP siguen en ElectronicDocuments; no se omite el validador al cambiar
el origen del XML. El manifest representa el esquema realmente habilitado, no una
activación preventiva desde documentación.

## Responsabilidad y límites

Preview y documento fiscal autorizado son lecturas distintas; no se inventa XML/PDF
autorizado cuando el origen aún no lo tiene. La decisión inicial de trigger manual
fue sustituida por la transmisión post-commit y recuperación actuales, no es una alternativa.
No cambia contratos públicos ni permite acceso cross-company desde Ride.

## SSOT y dependencias

[Backend — Retenciones](../architecture/backend.md#retenciones-una-capacidad-una-política-de-emisión-zh-retention-emission-ssot-01-zh-purchase-retention-confirm-01),
[RIDE](../architecture/backend.md#ride), [infraestructura fiscal](../architecture/frozen-infrastructure.md).
[ADR-036](ADR-036-retention-electronic-document-cancellation.md) y [ADR-043](ADR-043-background-processing-ssot.md)
conservan decisiones diferentes de ciclo fiscal y orquestación. Gates: [STATUS](../../STATUS.md).
