using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Retentions.DTOs;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Retentions.Enums;
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
    [InlineData(nameof(ExpensesController.GetRetentionEligibility), ExpensePermissions.DocumentsView)]
    [InlineData(nameof(ExpensesController.GetRetention), ExpensePermissions.DocumentsView)]
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
                    request.Notes,
                    request.TaxSupportCode
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
                    request.Notes,
                    request.TaxSupportCode
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

        var response = await controller.Confirm(id, request: null, CancellationToken.None);

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

        var response = await controller.Confirm(Guid.NewGuid(), request: null, CancellationToken.None);

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

        var response = await controller.Confirm(Guid.NewGuid(), request: null, CancellationToken.None);

        response.Should().NotBeOfType<OkObjectResult>();
        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Confirm_de_documento_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<ExpenseDocumentDetailDto>.NotFound("Gasto no encontrado.")
        );

        var response = await controller.Confirm(Guid.NewGuid(), request: null, CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── RETENTIONS-API-EXPENSES-01E ──────────────────────────────────────

    [Fact]
    public async Task GetRetentionEligibility_exitoso_retorna_200_y_envia_query()
    {
        object? sent = null;
        var id = Guid.NewGuid();
        var dto = SampleEligibility(id);
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<RetentionEligibilityDto>.Success(dto);
        });

        var response = await controller.GetRetentionEligibility(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent
            .Should()
            .BeEquivalentTo(
                new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, id)
            );
    }

    [Fact]
    public async Task GetRetentionEligibility_de_otra_company_o_branch_retorna_404()
    {
        // GetRetentionEligibilityHandler devuelve NotFound cuando el documento no existe en el
        // scope actual (otra company/branch tratado igual que "no existe", fail-closed) — el
        // controller solo propaga ese Result, sin lógica propia.
        var controller = BuildController(_ =>
            Result<RetentionEligibilityDto>.NotFound("Documento de gasto no encontrado.")
        );

        var response = await controller.GetRetentionEligibility(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Confirm_sin_body_no_envia_retencion_comportamiento_actual()
    {
        object? sent = null;
        var id = Guid.NewGuid();
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseDocumentDetailDto>.Success(
                SampleDetail(id) with { Status = ExpenseStatus.Confirmed }
            );
        });

        var response = await controller.Confirm(id, request: null, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeEquivalentTo(new ConfirmExpenseDocumentCommand(id));
    }

    [Fact]
    public async Task Confirm_con_RetentionIntent_mapea_1a1_al_command()
    {
        object? sent = null;
        var id = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseDocumentDetailDto>.Success(
                SampleDetail(id) with { Status = ExpenseStatus.Confirmed }
            );
        });
        var request = new ConfirmExpenseDocumentRequest(
            new RetentionIntentRequest(
                AppliesRetention: true,
                EmissionPointId: emissionPointId,
                IssueDate: new DateOnly(2026, 9, 3),
                Lines: new[]
                {
                    new RetentionIntentLineRequest(
                        RetentionTaxType.Vat,
                        "1",
                        100m,
                        70m,
                        70m,
                        "Retencion IVA"
                    ),
                }
            )
        );

        var response = await controller.Confirm(id, request, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent
            .Should()
            .BeEquivalentTo(
                new ConfirmExpenseDocumentCommand(
                    id,
                    new RetentionIntent(
                        AppliesRetention: true,
                        EmissionPointId: emissionPointId,
                        IssueDate: new DateOnly(2026, 9, 3),
                        Lines: new[]
                        {
                            new IssueRetentionLineInput(
                                RetentionTaxType.Vat,
                                "1",
                                100m,
                                70m,
                                70m,
                                "Retencion IVA"
                            ),
                        }
                    )
                )
            );
    }

    [Fact]
    public void ConfirmExpenseDocumentRequest_no_expone_TenantId_CompanyId_ni_BranchId()
    {
        // Test de forma del contrato: estructuralmente imposible enviar TenantId/CompanyId/BranchId
        // desde el body de confirmar — esos siguen viniendo exclusivamente del contexto autenticado
        // (ICurrentTenant/ICurrentCompany/ICurrentBranch) en el handler de Application.
        var requestProps = typeof(ConfirmExpenseDocumentRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();
        var intentProps = typeof(RetentionIntentRequest).GetProperties().Select(p => p.Name).ToArray();
        var lineProps = typeof(RetentionIntentLineRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();
        var forbidden = new[] { "TenantId", "CompanyId", "BranchId" };

        requestProps.Should().NotContain(forbidden);
        intentProps.Should().NotContain(forbidden);
        lineProps.Should().NotContain(forbidden);
    }

    [Fact]
    public async Task CreateConfirmed_con_RetentionIntent_mapea_1a1_al_command()
    {
        object? sent = null;
        var emissionPointId = Guid.NewGuid();
        var dto = SampleDetail(Guid.NewGuid());
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseDocumentDetailDto>.Success(dto);
        });
        var request = SampleCreateRequest() with
        {
            Retention = new RetentionIntentRequest(
                AppliesRetention: true,
                EmissionPointId: emissionPointId,
                IssueDate: new DateOnly(2026, 9, 3),
                Lines: new[]
                {
                    new RetentionIntentLineRequest(RetentionTaxType.Income, "303", 50m, 1m, 0.5m),
                }
            ),
        };

        var response = await controller.CreateConfirmed(request, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sent
            .Should()
            .BeEquivalentTo(
                new CreateConfirmedExpenseCommand(
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
                    request.Notes,
                    request.TaxSupportCode,
                    new RetentionIntent(
                        AppliesRetention: true,
                        EmissionPointId: emissionPointId,
                        IssueDate: new DateOnly(2026, 9, 3),
                        Lines: new[]
                        {
                            new IssueRetentionLineInput(
                                RetentionTaxType.Income,
                                "303",
                                50m,
                                1m,
                                0.5m,
                                null
                            ),
                        }
                    )
                )
            );
    }

    [Fact]
    public async Task GetRetention_existente_retorna_200_con_dto()
    {
        var id = Guid.NewGuid();
        var dto = SampleRetention(id);
        var controller = BuildController(_ => Result<RetentionDocumentDto?>.Success(dto));

        var response = await controller.GetRetention(id, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRetention_inexistente_o_de_otra_company_branch_retorna_404()
    {
        // GetRetentionBySourceHandler nunca devuelve NotFound (Success(null) es el estado normal
        // de "sin retención activa", incluida la retención de otra company/branch tratada igual
        // que "no existe" — fail-closed). El controller traduce ese null a 404.
        var controller = BuildController(_ => Result<RetentionDocumentDto?>.Success(null));

        var response = await controller.GetRetention(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetRetention_envia_la_query_correcta_con_el_id_de_ruta()
    {
        object? sent = null;
        var id = Guid.NewGuid();
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<RetentionDocumentDto?>.Success(null);
        });

        await controller.GetRetention(id, CancellationToken.None);

        sent
            .Should()
            .BeEquivalentTo(
                new GetRetentionBySourceQuery(RetentionSourceDocumentType.ExpenseDocument, id)
            );
    }

    [Fact]
    public async Task Cancel_con_retencion_activa_reversada_retorna_200()
    {
        // CancelExpenseDocumentHandler ya reversa la retención completa internamente
        // (RETENTIONS-EXPENSES-CANCEL-REVERSAL-01D-3) — el controller solo verifica que un
        // Result exitoso se traduzca a 200, sin lógica propia.
        object? sent = null;
        var id = Guid.NewGuid();
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<ExpenseDocumentDetailDto>.Success(
                SampleDetail(id) with { Status = ExpenseStatus.Cancelled }
            );
        });

        var response = await controller.Cancel(
            id,
            new CancelExpenseDocumentRequest("Anulado por error de captura"),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sent
            .Should()
            .BeEquivalentTo(
                new CancelExpenseDocumentCommand(id, "Anulado por error de captura")
            );
    }

    [Fact]
    public async Task Cancel_con_AP_pagada_bloqueada_retorna_422()
    {
        // CancelExpenseDocumentHandler bloquea con ValidationFailure cuando la CxP ya tiene pagos
        // aplicados (AccountsPayable.Cancel() lanza InvalidOperationException) — mismo patrón ya
        // usado por el resto del controller, mapeado a 422 (UnprocessableEntity).
        var controller = BuildController(_ =>
            Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "No se puede anular una cuenta por pagar con pagos aplicados."
            )
        );

        var response = await controller.Cancel(
            Guid.NewGuid(),
            new CancelExpenseDocumentRequest("Anulado por error de captura"),
            CancellationToken.None
        );

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    private static RetentionEligibilityDto SampleEligibility(Guid id) =>
        new(
            RetentionSourceDocumentType.ExpenseDocument,
            id,
            IsSupportedInThisPhase: true,
            CanRetainVat: true,
            CanRetainIncome: true,
            IsSupplierExempt: false,
            HasRetainableBase: true,
            MissingRetentionCode: false,
            IsSupplierRequiredToKeepAccounting: false,
            SuggestedVatRetentionCode: "1",
            SuggestedIncomeRetentionCode: "303",
            Reasons: Array.Empty<string>()
        );

    private static RetentionDocumentDto SampleRetention(Guid sourceId) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "001-001-000000123",
            new DateOnly(2026, 9, 3),
            RetentionStatus.Issued,
            70m,
            0m,
            70m,
            null,
            null,
            null,
            Array.Empty<RetentionDocumentLineDto>()
        );

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
            Notes: "Borrador",
            TaxSupportCode: "02"
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
            Notes: "Borrador editado",
            TaxSupportCode: "02"
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
            null,
            ExpenseStatus.Draft,
            Array.Empty<ExpenseLineDto>(),
            null,
            null,
            null
        );
}
