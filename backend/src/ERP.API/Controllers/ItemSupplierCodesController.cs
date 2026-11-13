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

    /// <summary>
    /// Asocia un código de proveedor a una presentación del ítem.
    /// </summary>
    /// <remarks>
    /// Identifica el código por proveedor y código, y lo vincula con un nivel de empaque activo del mismo ítem.
    /// </remarks>
    /// <param name="id">Id de el ítem.</param>
    /// <param name="request">Proveedor, código y presentación (nivel de empaque) a asociar.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Código de proveedor actualizado.</response>
    /// <response code="404">El recurso indicado no existe o no es visible para la empresa activa.</response>
    [HttpPut("{id:guid}/supplier-codes/presentation")]
    [Authorize(Policy = $"perm:{InventoryPermissions.ItemsEdit}")]
    [ProducesResponseType(typeof(ApiResponse<ItemDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
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
