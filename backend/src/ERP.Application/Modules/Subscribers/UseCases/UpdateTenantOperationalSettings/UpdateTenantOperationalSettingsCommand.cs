using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;
using MediatR;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberOperationalSettings;

/// <summary>
/// Actualiza los parámetros operativos de la empresa: moneda, idioma, zona horaria,
/// prefijo de factura y días de crédito por defecto.
/// Accesible por el administrador de la propia empresa o por operador platform.
/// </summary>
public record UpdateSubscriberOperationalSettingsCommand(
    Guid SubscriberId,
    string Currency,
    string Language,
    string Timezone,
    string? InvoicePrefix,
    int DefaultCreditDays) : IRequest<Result<SubscriberDto>>;
