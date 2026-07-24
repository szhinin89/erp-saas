using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.ActivateBusinessPartner;

public sealed record ActivateBusinessPartnerCommand(Guid Id)
    : IRequest<Result<bool>>, ITenantScopedRequest;
