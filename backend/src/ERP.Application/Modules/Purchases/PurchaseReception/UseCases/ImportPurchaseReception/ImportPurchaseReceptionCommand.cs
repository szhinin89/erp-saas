using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;

/// <summary>
/// Ahora persiste — cada Factura del TXT queda como <c>PurchaseReceptionDocument</c> de la sucursal
/// activa (Branch Ownership Rule). <see cref="IBranchScopedRequest"/> exige que exista contexto de
/// sucursal antes de ejecutar el handler; BranchId nunca se acepta desde el cliente.
/// </summary>
public sealed record ImportPurchaseReceptionCommand(MediaUploadContent File)
    : IRequest<Result<PurchaseReceptionImportResultDto>>, IBranchScopedRequest;
