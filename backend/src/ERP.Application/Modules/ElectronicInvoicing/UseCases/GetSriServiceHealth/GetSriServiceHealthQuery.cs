using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSriServiceHealth;

/// <summary>
/// Salud técnica de Recepción y Autorización del SRI para el ambiente de la empresa actual. Solo
/// lee el monitor en memoria: nunca ejecuta SOAP, ping ni envíos.
/// </summary>
public sealed record GetSriServiceHealthQuery
    : IRequest<Result<SriServiceHealthDto>>,
        IRequiresCompanyContext;
