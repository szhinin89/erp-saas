using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// Criterios de selección de plantilla (ADR-025 §11). Los campos jerárquicos son opcionales
/// desde el diseño v1.0 aunque hoy <see cref="IRideTemplateResolver"/> solo resuelva por
/// <see cref="DocumentType"/> — la jerarquía Empresa→Sucursal→Punto de Emisión→Tipo se activa
/// cambiando la implementación del resolver, nunca este selector. Únicamente datos: no
/// implementa ninguna lógica de selección.
/// </summary>
public sealed record RideTemplateSelector(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid? EmissionPointId,
    RideDocumentType DocumentType);
