using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.ObtenerProveedor;

public record GetSupplierByIdQuery(Guid Id)
    : IRequest<Result<SupplierDetailDto?>>, ICompanyScopedRequest;
