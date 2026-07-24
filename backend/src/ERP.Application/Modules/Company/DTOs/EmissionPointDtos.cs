using ERP.Domain.Modules.Company.Enums;

namespace ERP.Application.Modules.Company.DTOs;

/// <summary>DTO detallado para operaciones CRUD por establecimiento (patrón existente).</summary>
public record EmissionPointDto(
    Guid         Id,
    Guid         EstablishmentId,
    Guid         CompanyId,
    string       Code,
    string?      Name,
    EmissionType EmissionType,
    bool         IsDefault,
    bool         IsActive,
    DateTime     CreatedAt,
    DateTime?    UpdatedAt);

/// <summary>DTO enriquecido para el listado independiente — incluye nombre y código del establecimiento.</summary>
public record EmissionPointListItemDto(
    Guid         Id,
    Guid         EstablishmentId,
    string       EstablishmentCode,
    string       EstablishmentName,
    string?      BranchName,
    string       Code,
    string?      Name,
    EmissionType EmissionType,
    bool         IsDefault,
    bool         IsActive,
    DateTime     CreatedAt);

/// <summary>DTO de lookup para poblar el selector de Establecimiento en el formulario.</summary>
public record EstablishmentLookupDto(
    Guid   Id,
    string Code,
    string Name);
