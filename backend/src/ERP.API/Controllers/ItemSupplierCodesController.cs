using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.ItemSupplierCodes;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/v1/items")]
[Authorize]
[Produces("application/json")]
public sealed class ItemSupplierCodesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemSupplierCodesController(IMediator mediator) => _mediator = mediator;

    [HttpPut("{id:guid}/supplier-codes/presentation")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSupplierCodePresentation(
        Guid id,
        [FromBody] UpdateSupplierCodePresentationRequest request,
        CancellationToken cancellationToken
    ) =>
        this.ToOkOrBadRequest(
            await _mediator.Send(
                new UpdateItemSupplierCodePackagingCommand(
                    id,
                    request.SupplierId,
                    request.Code,
                    request.PackagingLevelId
                ),
                cancellationToken
            )
        );
}

public sealed record UpdateSupplierCodePresentationRequest(
    Guid SupplierId,
    string Code,
    Guid? PackagingLevelId
);
