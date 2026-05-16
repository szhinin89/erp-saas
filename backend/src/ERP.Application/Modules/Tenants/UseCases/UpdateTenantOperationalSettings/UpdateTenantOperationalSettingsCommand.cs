using ERP.Application.Common;
using ERP.Application.Tenants.DTOs;
using MediatR;

namespace ERP.Application.Tenants.UseCases.UpdateTenantOperationalSettings;

/// <summary>
/// Actualiza los parámetros operativos de la empresa: moneda, idioma, zona horaria,
/// prefijo de factura y días de crédito por defecto.
/// Accesible por el administrador de la propia empresa o por SuperAdmin.
/// </summary>
public record UpdateTenantOperationalSettingsCommand(
    Guid TenantId,
    string Currency,
    string Language,
    string Timezone,
    string? InvoicePrefix,
    int DefaultCreditDays) : IRequest<Result<TenantDto>>;
