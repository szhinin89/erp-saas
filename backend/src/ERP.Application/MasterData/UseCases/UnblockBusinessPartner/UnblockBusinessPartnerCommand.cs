using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UnblockBusinessPartner;

public sealed record UnblockBusinessPartnerCommand(Guid BusinessPartnerId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;
