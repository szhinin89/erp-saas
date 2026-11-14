using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetSupplierExpenseSubcategories;

/// <summary>
/// EXPENSE-SUPPLIER-SUBCATEGORIES-01 — modo y asignaciones de subcategorías de gasto de un
/// proveedor en la empresa activa. Company-scoped (company_id solo del contexto).
/// </summary>
public sealed record GetSupplierExpenseSubcategoriesQuery(Guid BusinessPartnerId)
    : IRequest<Result<SupplierExpenseSubcategoriesDto>>,
        ICompanyScopedRequest;
