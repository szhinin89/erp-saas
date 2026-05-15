using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.ListarProveedores;

public record GetSuppliersQuery(
    bool?   ActiveFilter,
    string? Search,
    string?   PersonType
) : IRequest<Result<IReadOnlyList<SupplierDto>>>;
