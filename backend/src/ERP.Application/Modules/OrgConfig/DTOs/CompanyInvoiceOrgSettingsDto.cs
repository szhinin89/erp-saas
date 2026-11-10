namespace ERP.Application.Modules.OrgConfig.DTOs;

/// <summary>
/// Preferencias de venta a nivel empresa (Configuración → Ventas).
/// Propietario: Empresa. Null = sin configurar — el usuario elige manualmente en cada factura.
/// <see cref="DefaultPriceListId"/> es una proyección de <c>PriceList.IsDefault</c> (lista activa),
/// no un OrgSetting. <see cref="ConfigurationWarnings"/> explica valores guardados que ya no son
/// válidos (se conservan, pero Ventas los ignora). <see cref="AllowedDocTypeCodes"/> proyecta la
/// política vigente de <c>IInvoiceDefaultsCatalogValidator</c> para filtrar el selector.
/// DefaultWarehouseId es propiedad de Sucursal; DefaultEmissionPointId usa EmissionPoint.IsDefault.
/// </summary>
public sealed record CompanyInvoiceOrgSettingsDto(
    string? DefaultDocTypeCode,
    string? DefaultSriPaymentMethodCode,
    Guid? DefaultPaymentTermId,
    Guid? DefaultPriceListId,
    IReadOnlyList<string> ConfigurationWarnings,
    IReadOnlyList<string> AllowedDocTypeCodes
);
