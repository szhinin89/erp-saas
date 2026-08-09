namespace ERP.Application.MasterData.DTOs;

/// <summary>
/// DTO compartido por los 12 catálogos de clasificación de BusinessPartner
/// (CLASS-BP-CATALOGS-01): CustomerCategory, CustomerSegment, CustomerCreditRating, LoyaltyTier,
/// CustomerInvoiceFormat, CustomerClassification, SupplierCategory, SupplierType, SupplierRisk,
/// SupplierRating, PrimaryGoodType, SupplierSegment. Solo lectura — sin campos de auditoría ni
/// IsSystemSeeded porque el frontend no los necesita (CRUD administrativo queda fuera de alcance).
/// </summary>
public sealed record ClassificationCatalogItemDto(Guid Id, string Code, string Name, int SortOrder);
