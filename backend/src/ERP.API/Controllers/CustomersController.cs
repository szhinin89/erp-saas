using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.UseCases.CrearCliente;
using ERP.Application.Modules.Sales.UseCases.DeshabilitarCliente;
using ERP.Application.Modules.Sales.UseCases.HabilitarCliente;
using ERP.Application.Modules.Sales.UseCases.ObtenerCliente;
using ERP.Application.Modules.Sales.UseCases.ListarClientes;
using ERP.Application.Modules.Sales.UseCases.ActualizarCliente;
using ERP.API.Attributes;

namespace ERP.API.Controllers;

[Modulo("Clientes", "perm:ventas.customers.view", "👤", "/ventas/clientes", "perm:ventas.facturas.view", 52)]
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class CustomersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "perm:ventas.customers.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(new GetCustomersQuery(activeFilter, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<CustomerDto>());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:ventas.customers.view")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCustomerByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost]
    [Authorize(Policy = "perm:ventas.customers.create")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto?>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Creado");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:ventas.customers.update")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("El id de ruta no coincide con el cuerpo.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:ventas.customers.delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableCustomerCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Deshabilitado");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:ventas.customers.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableCustomerCommand(id), ct);
        return this.ToOkOrBadRequest(result, "Habilitado");
    }
}

