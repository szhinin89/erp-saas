using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Purchasing.UseCases.Retentions;

public sealed record GenerateIssuedRetentionCommand(Guid PurchBillId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
