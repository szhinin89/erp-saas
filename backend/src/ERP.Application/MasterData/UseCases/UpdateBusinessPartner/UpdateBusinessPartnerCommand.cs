using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UpdateBusinessPartner;

public sealed record UpdateBusinessPartnerCommand(
    Guid    Id,
    string  IdentificationType,
    string  IdentificationNumber,
    string  LegalName,
    string? TradeName,
    string? Email,
    string? Phone,
    string? CountryCode) : IRequest<Result<bool>>, ISubscriberOnlyRequest;
