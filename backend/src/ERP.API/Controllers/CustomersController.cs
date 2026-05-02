using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.Application.Modules.Customers.DTOs;
using ERP.Application.Modules.Customers.UseCases.CreateCustomer;
using ERP.Application.Modules.Customers.UseCases.DisableCustomer;
using ERP.Application.Modules.Customers.UseCases.EnableCustomer;
using ERP.Application.Modules.Customers.UseCases.GetCustomerById;
using ERP.Application.Modules.Customers.UseCases.GetCustomers;
using ERP.Application.Modules.Customers.UseCases.UpdateCustomer;

namespace ERP.API.Controllers;

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
    [Authorize(Policy = "perm:catalog.customers.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(new GetCustomersQuery(activeFilter, search), ct);
        return Ok(new ApiResponse<IReadOnlyList<CustomerDto>>(
            result.IsSuccess,
            result.IsSuccess ? "OK" : result.Error ?? "Error",
            result.Value ?? Array.Empty<CustomerDto>()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:catalog.customers.view")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCustomerByIdQuery(id), ct);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object?>(false, result.Error ?? "No encontrado", null));

        return Ok(new ApiResponse<CustomerDetailDto?>(true, "OK", result.Value));
    }

    [HttpPost]
    [Authorize(Policy = "perm:catalog.customers.create")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new ApiResponse<CustomerDto?>(true, "Creado", result.Value))
            : BadRequest(new ApiResponse<object?>(false, result.Error ?? "Error", null));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:catalog.customers.update")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerCommand command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return BadRequest(new ApiResponse<object?>(false, "El id de ruta no coincide con el cuerpo.", null));

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<CustomerDto?>(true, "OK", result.Value))
            : BadRequest(new ApiResponse<object?>(false, result.Error ?? "Error", null));
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:catalog.customers.delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DisableCustomerCommand(id), ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<CustomerDto?>(true, "Deshabilitado", result.Value))
            : BadRequest(new ApiResponse<object?>(false, result.Error ?? "Error", null));
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:catalog.customers.update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EnableCustomerCommand(id), ct);
        return result.IsSuccess
            ? Ok(new ApiResponse<CustomerDto?>(true, "Habilitado", result.Value))
            : BadRequest(new ApiResponse<object?>(false, result.Error ?? "Error", null));
    }
}
