using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Controllers;
using ERP.API.Contracts;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Application.Modules.Branches.UseCases.CreateBranch;
using ERP.Application.Modules.Ventas.DTOs;
using ERP.Application.Modules.Ventas.UseCases.CrearCliente;
using ERP.Application.Modules.Ventas.UseCases.ObtenerCliente;

namespace ERP.API.Tests;

public class ControllerContractTests
{
    [Fact]
    public async Task Customers_GetById_WhenMissing_Returns404Contract()
    {
        var mediator = new StubMediator(request =>
        {
            if (request is GetCustomerByIdQuery)
                return Result<CustomerDetailDto>.Failure("Cliente no encontrado.");

            throw new InvalidOperationException("Request no configurado en test.");
        });

        var controller = new CustomersController(mediator);
        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);

        var payload = notFound.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        payload.Success.Should().BeFalse();
        payload.Message.Should().Be("Cliente no encontrado.");
    }

    [Fact]
    public async Task Branches_Create_WhenSuccess_Returns201Contract()
    {
        var expected = new BranchDto(
            Guid.NewGuid(), "Matriz", "Av. Principal", null, null, null, null, null, null, null, null, null, true, true);

        var mediator = new StubMediator(request =>
        {
            if (request is CreateBranchCommand)
                return Result<BranchDto>.Success(expected);

            throw new InvalidOperationException("Request no configurado en test.");
        });

        var controller = new BranchesController(mediator);
        var command = new CreateBranchCommand("Matriz", "Av. Principal", null, null, null, null, null, null, null, null, null, true, true);
        var result = await controller.Create(command, CancellationToken.None);

        var created = result.Should().BeOfType<ObjectResult>().Subject;
        created.StatusCode.Should().Be(201);

        var payload = created.Value.Should().BeOfType<ApiResponse<BranchDto>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Message.Should().Be("Creado");
        payload.ResponseObject.Should().Be(expected);
    }

    [Fact]
    public async Task Customers_Create_WhenSuccess_Returns201Contract()
    {
        var expected = new CustomerDto(
            Guid.NewGuid(), "RUC", "0999999999001", "Empresa Demo", null, null, null, null, null, true);

        var mediator = new StubMediator(request =>
        {
            if (request is CreateCustomerCommand)
                return Result<CustomerDto>.Success(expected);

            throw new InvalidOperationException("Request no configurado en test.");
        });

        var controller = new CustomersController(mediator);
        var command = new CreateCustomerCommand("RUC", "0999999999001", "Empresa Demo", null, null, null, null, null, true);
        var result = await controller.Create(command, CancellationToken.None);

        var created = result.Should().BeOfType<ObjectResult>().Subject;
        created.StatusCode.Should().Be(201);

        var payload = created.Value.Should().BeOfType<ApiResponse<CustomerDto>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Message.Should().Be("Creado");
        payload.ResponseObject.Should().Be(expected);
    }

    private sealed class StubMediator : IMediator
    {
        private readonly Func<object, object> _handler;

        public StubMediator(Func<object, object> handler)
        {
            _handler = handler;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((TResponse)_handler(request));
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((object?)_handler(request));
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            _ = _handler(request!);
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<object?>();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
