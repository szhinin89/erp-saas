using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Purchasing.UseCases.Retenciones;

public sealed record GenerateIssuedRetentionCommand(Guid PurchBillId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
