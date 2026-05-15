using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.RetencionesRecibidas;

public sealed record RegistrarSalesRetentionCommand(
    Guid SalesBillId,
    string XmlContent) : IRequest<Result<Guid>>;
