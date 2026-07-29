namespace ERP.Application.Modules.OrgConfig.DTOs;

/// <summary>
/// Configuraciones de factura de venta a nivel empresa.
/// Propietario: Empresa. Administración: Empresa → Configuraciones.
/// Null = sin configurar — el usuario elige manualmente en cada factura.
/// DefaultWarehouseId es propiedad de Sucursal; DefaultEmissionPointId usa EmissionPoint.IsDefault.
/// </summary>
public sealed record CompanyInvoiceOrgSettingsDto(
    string? DefaultDocTypeCode,
    string? DefaultSriPaymentMethodCode,
    Guid? DefaultPaymentTermId
);
