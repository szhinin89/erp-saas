namespace ERP.Domain.MasterData.Interfaces;

/// <summary>
/// Contrato común de los 12 catálogos de clasificación de BusinessPartner (CLASS-BP-CATALOGS-01):
/// CustomerCategory, CustomerSegment, CustomerCreditRating, LoyaltyTier, CustomerInvoiceFormat,
/// CustomerClassification, SupplierCategory, SupplierType, SupplierRisk, SupplierRating,
/// PrimaryGoodType, SupplierSegment. Permite que <c>ClassificationCatalogRepositoryBase&lt;TEntity&gt;</c>
/// (Infrastructure) implemente la lectura/validación una sola vez sin reflexión.
/// </summary>
public interface IClassificationCatalogEntity
{
    string Code { get; }
    string Name { get; }
    int SortOrder { get; }
}
