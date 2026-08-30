using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Expenses.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Expenses;

public sealed class ExpensesControllerTests
{
    private static ExpensesController BuildController(Func<object, object> handler)
    {
        var controller = new ExpensesController(new StubMediator(handler));
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(),
            },
        };
        return controller;
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
    }

    [Fact]
    public void El_controlador_exige_autenticacion()
    {
        typeof(ExpensesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .ContainSingle();
    }

    [Theory]
    [InlineData(nameof(ExpensesController.GetList), ExpensePermissions.DocumentsView)]
    [InlineData(nameof(ExpensesController.GetById), ExpensePermissions.DocumentsView)]
    [InlineData(nameof(ExpensesController.CreateDraft), ExpensePermissions.DocumentsCreate)]
    [InlineData(nameof(ExpensesController.UpdateDraft), ExpensePermissions.DocumentsUpdate)]
    [InlineData(nameof(ExpensesController.Confirm), ExpensePermissions.DocumentsConfirm)]
    [InlineData(nameof(ExpensesController.Cancel), ExpensePermissions.DocumentsCancel)]
    public void Cada_endpoint_expone_su_permiso_propio(string methodName, string permission)
    {
        var method = typeof(ExpensesController).GetMethod(methodName)!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{permission}");
    }

    [Fact]
    public async Task GetList_exitoso_retorna_200_y_envia_filtros()
    {
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseDocumentListResponse>.Success(
                new ExpenseDocumentListResponse(Array.Empty<ExpenseDocumentListItemDto>(), 0, 2, 10)
            );
        });

        var response = await controller.GetList("prov", "Draft", 2, 10, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeEquivalentTo(new ListExpenseDocumentsQuery("prov", "Draft", 2, 10));
    }

    [Fact]
    public async Task CreateDraft_exitoso_retorna_201_y_envia_command()
    {
        object? sent = null;
        var dto = SampleDetail(Guid.NewGuid());
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseDocumentDetailDto>.Success(dto);
        });
        var request = SampleCreateRequest();

        var response = await controller.CreateDraft(request, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sent
            .Should()
            .BeEquivalentTo(
                new CreateExpenseDraftCommand(
                    request.SupplierId,
                    request.IssueDate,
                    request.AccountingDate,
                    request.DocumentType,
                    request.DocumentNumber,
                    request.PaymentTermId,
                    request.DueDate,
                    request.Lines,
                    request.AuthorizationNumber,
                    request.AuthorizationDate,
                    request.Notes
                )
            );
    }

    [Fact]
    public async Task UpdateDraft_exitoso_usa_el_id_de_ruta()
    {
        object? sent = null;
        var id = Guid.NewGuid();
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseDocumentDetailDto>.Success(SampleDetail(id));
        });
        var request = SampleUpdateRequest();

        var response = await controller.UpdateDraft(id, request, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent
            .Should()
            .BeEquivalentTo(
                new UpdateExpenseDraftCommand(
                    id,
                    request.SupplierId,
                    request.IssueDate,
                    request.AccountingDate,
                    request.DocumentType,
                    request.DocumentNumber,
                    request.PaymentTermId,
                    request.DueDate,
                    request.Lines,
                    request.AuthorizationNumber,
                    request.AuthorizationDate,
                    request.Notes
                )
            );
    }

    [Fact]
    public async Task CreateDraft_sin_lineas_retorna_422_si_handler_valida()
    {
        var controller = BuildController(_ =>
            Result<ExpenseDocumentDetailDto>.ValidationFailure("Debe incluir al menos una linea.")
        );

        var response = await controller.CreateDraft(SampleCreateRequest(), CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task Confirm_exitoso_retorna_200_y_envia_command_con_el_id_de_ruta()
    {
        object? sent = null;
        var id = Guid.NewGuid();
        var dto = SampleDetail(id) with { Status = ExpenseStatus.Confirmed };
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseDocumentDetailDto>.Success(dto);
        });

        var response = await controller.Confirm(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeEquivalentTo(new ConfirmExpenseDocumentCommand(id));
    }

    [Fact]
    public async Task Confirm_de_documento_no_Draft_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "Solo se pueden confirmar gastos en estado borrador."
            )
        );

        var response = await controller.Confirm(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task Confirm_con_posting_fallido_no_retorna_200()
    {
        // El codigo de fallo de posting (p. ej. "RULE_NOT_FOUND") se propaga tal cual desde
        // IPostingEngine — no es el generico ValidationError, por lo que ApiResultExtensions lo
        // mapea a 400, no 422. Lo que importa para EXPENSES-CONFIRM-07 es que nunca sea 200.
        var controller = BuildController(_ =>
            Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "No existe regla de contabilizacion.",
                "RULE_NOT_FOUND"
            )
        );

        var response = await controller.Confirm(Guid.NewGuid(), CancellationToken.None);

        response.Should().NotBeOfType<OkObjectResult>();
        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Confirm_de_documento_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<ExpenseDocumentDetailDto>.NotFound("Gasto no encontrado.")
        );

        var response = await controller.Confirm(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    private static CreateExpenseDraftRequest SampleCreateRequest()
    {
        var line = new ExpenseDraftLineRequest(Guid.NewGuid(), "Internet", 1m, 100m);
        return new CreateExpenseDraftRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 27),
            new DateOnly(2026, 8, 27),
            "01",
            "001-001-000000001",
            Guid.NewGuid(),
            null,
            new[] { line },
            Notes: "Borrador"
        );
    }

    private static UpdateExpenseDraftRequest SampleUpdateRequest()
    {
        var line = new ExpenseDraftLineRequest(Guid.NewGuid(), "Internet", 1m, 100m);
        return new UpdateExpenseDraftRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 27),
            new DateOnly(2026, 8, 27),
            "01",
            "001-001-000000001",
            Guid.NewGuid(),
            null,
            new[] { line },
            Notes: "Borrador editado"
        );
    }

    private static ExpenseDocumentDetailDto SampleDetail(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Proveedor",
            "1791352688001",
            new DateOnly(2026, 8, 27),
            new DateOnly(2026, 8, 27),
            "01",
            "001-001-000000001",
            null,
            null,
            Guid.NewGuid(),
            "Credito 30 dias",
            null,
            100m,
            0m,
            0m,
            100m,
            null,
            ExpenseStatus.Draft,
            Array.Empty<ExpenseLineDto>(),
            null,
            null,
            null
        );
}
