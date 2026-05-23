using ERP.Application.Common;
using ERP.Application.Modules.Logistics.DTOs;
using MediatR;

namespace ERP.Application.Modules.Logistics.UseCases.CreateCarrier;

[RequireFeature(SubscriptionFeatureCodes.Logistics)]
public record CreateCarrierCommand(
    string  IdentificationType,
    string  IdentificationNumber,
    string  LegalName,
    string  LicensePlate,
    string? Phone,
    string? Email) : IRequest<Result<CarrierDto>>, ICompanyScopedRequest;
