namespace ERP.Application.Modules.OrgConfig.DTOs;

/// <summary>
/// Configuraciones de factura de venta a nivel sucursal.
/// Propietario: Sucursal. Administración: Editar Sucursal → Configuraciones.
/// Null = sin configurar — fallback a la primera bodega activa de la empresa.
/// </summary>
public sealed record BranchInvoiceOrgSettingsDto(
    Guid? DefaultWarehouseId
);
