using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Finance.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Finance;

/// <summary>
/// P0-02 Fase 4 (reejecución) — contrato de <see cref="CompanyFinancialDestinationController"/>
/// con <c>StubMediator</c>, mismo alcance que <c>FinancePaymentsControllerTests</c>: mapeo de
/// Command a HTTP, verificación por reflexión de la policy declarada, y pruebas negativas de
/// superficie que confirman que los 3 contratos de actualización no aceptan campos estructurales.
/// </summary>
public sealed class CompanyFinancialDestinationControllerTests
{
    private static CompanyFinancialDestinationController BuildController(
        Func<object, object> handler
    )
    {
        var controller = new CompanyFinancialDestinationController(new StubMediator(handler));
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

    private static CompanyFinancialDestinationDto SampleDto(Guid id) =>
        new(
            id,
            "BANCO-001",
            "Cuenta corriente Pichincha",
            nameof(FinancialDestinationTypeCode.BankAccount),
            Guid.NewGuid(),
            "USD",
            null,
            "PICHINCHA",
            "2200123456",
            true
        );

    // ── Autorización declarativa ─────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(CompanyFinancialDestinationController.Create))]
    [InlineData(nameof(CompanyFinancialDestinationController.Rename))]
    [InlineData(nameof(CompanyFinancialDestinationController.ChangeAccountingAccount))]
    [InlineData(nameof(CompanyFinancialDestinationController.SetActive))]
    public void Cada_endpoint_exige_perm_settings_financial_destinations_manage(string methodName)
    {
        var method = typeof(CompanyFinancialDestinationController).GetMethod(methodName)!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{SettingsPermissions.FinancialDestinationsManage}");
    }

    [Fact]
    public void El_controlador_exige_autenticacion()
    {
        typeof(CompanyFinancialDestinationController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should()
            .ContainSingle();
    }

    // ── POST / (crear) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_exitoso_retorna_201_y_envia_el_command_recibido()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CompanyFinancialDestinationDto>.Success(SampleDto(id));
        });
        var command = new CreateCompanyFinancialDestinationCommand(
            "BANCO-001",
            "Cuenta corriente Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            Guid.NewGuid(),
            "USD",
            null,
            "PICHINCHA",
            "2200123456"
        );

        var response = await controller.Create(command, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sentRequest.Should().Be(command);
    }

    [Fact]
    public async Task Create_con_configuracion_incompleta_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<CompanyFinancialDestinationDto>.ValidationFailure(
                "La institución bancaria es obligatoria para un destino tipo banco."
            )
        );
        var command = new CreateCompanyFinancialDestinationCommand(
            "BANCO-002",
            "Cuenta sin institución",
            FinancialDestinationTypeCode.BankAccount,
            Guid.NewGuid(),
            "USD",
            null,
            null,
            "2200123456"
        );

        var response = await controller.Create(command, CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task Create_con_cuenta_contable_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<CompanyFinancialDestinationDto>.NotFound(
                "La cuenta contable indicada no existe o no pertenece a esta empresa."
            )
        );
        var command = new CreateCompanyFinancialDestinationCommand(
            "BANCO-001",
            "Cuenta corriente Pichincha",
            FinancialDestinationTypeCode.BankAccount,
            Guid.NewGuid(),
            "USD",
            null,
            "PICHINCHA",
            "2200123456"
        );

        var response = await controller.Create(command, CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── POST /{id}/rename ────────────────────────────────────────────────────

    [Fact]
    public async Task Rename_exitoso_retorna_200_y_envia_solo_el_Id_y_el_Name()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CompanyFinancialDestinationDto>.Success(SampleDto(id));
        });

        var response = await controller.Rename(
            id,
            new RenameCompanyFinancialDestinationRequest("Nueva razón social visible"),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sentRequest
            .Should()
            .BeEquivalentTo(
                new UpdateCompanyFinancialDestinationNameCommand(id, "Nueva razón social visible")
            );
    }

    [Fact]
    public async Task Rename_sobre_destino_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<CompanyFinancialDestinationDto>.NotFound("Destino financiero no encontrado.")
        );

        var response = await controller.Rename(
            Guid.NewGuid(),
            new RenameCompanyFinancialDestinationRequest("Nuevo nombre"),
            CancellationToken.None
        );

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void El_contrato_de_Rename_no_expone_cuenta_estado_ni_campos_estructurales()
    {
        var properties = typeof(RenameCompanyFinancialDestinationRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        properties.Should().BeEquivalentTo(new[] { "Name" });
    }

    // ── POST /{id}/change-accounting-account ────────────────────────────────

    [Fact]
    public async Task ChangeAccountingAccount_exitoso_retorna_200_y_envia_solo_el_Id_y_la_cuenta()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CompanyFinancialDestinationDto>.Success(SampleDto(id));
        });

        var response = await controller.ChangeAccountingAccount(
            id,
            new ChangeCompanyFinancialDestinationAccountingAccountRequest(accountId),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sentRequest
            .Should()
            .BeEquivalentTo(
                new ChangeCompanyFinancialDestinationAccountingAccountCommand(id, accountId)
            );
    }

    [Fact]
    public async Task ChangeAccountingAccount_con_cuenta_no_postable_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<CompanyFinancialDestinationDto>.ValidationFailure(
                "La cuenta contable indicada no es postable o está inactiva."
            )
        );

        var response = await controller.ChangeAccountingAccount(
            Guid.NewGuid(),
            new ChangeCompanyFinancialDestinationAccountingAccountRequest(Guid.NewGuid()),
            CancellationToken.None
        );

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public void El_contrato_de_ChangeAccountingAccount_no_expone_nombre_estado_ni_campos_estructurales()
    {
        var properties = typeof(ChangeCompanyFinancialDestinationAccountingAccountRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        properties.Should().BeEquivalentTo(new[] { "AccountingAccountId" });
    }

    // ── POST /{id}/set-active ────────────────────────────────────────────────

    [Fact]
    public async Task SetActive_exitoso_retorna_200_y_envia_solo_el_Id_y_el_flag()
    {
        var id = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CompanyFinancialDestinationDto>.Success(SampleDto(id));
        });

        var response = await controller.SetActive(
            id,
            new SetCompanyFinancialDestinationActiveRequest(false),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sentRequest
            .Should()
            .BeEquivalentTo(new SetCompanyFinancialDestinationActiveCommand(id, false));
    }

    [Fact]
    public async Task SetActive_sobre_destino_inexistente_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<CompanyFinancialDestinationDto>.NotFound("Destino financiero no encontrado.")
        );

        var response = await controller.SetActive(
            Guid.NewGuid(),
            new SetCompanyFinancialDestinationActiveRequest(false),
            CancellationToken.None
        );

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void El_contrato_de_SetActive_no_expone_nombre_cuenta_ni_campos_estructurales()
    {
        var properties = typeof(SetCompanyFinancialDestinationActiveRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        properties.Should().BeEquivalentTo(new[] { "IsActive" });
    }

    // ── Ausencia de superficie no autorizada ─────────────────────────────────

    [Fact]
    public void El_controlador_no_expone_DELETE()
    {
        var methods = typeof(CompanyFinancialDestinationController).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );

        methods
            .SelectMany(m =>
                m.GetCustomAttributes(
                    typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute),
                    inherit: true
                )
            )
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void El_controlador_no_expone_un_PUT_generico_de_actualizacion()
    {
        var methods = typeof(CompanyFinancialDestinationController).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );

        methods
            .SelectMany(m =>
                m.GetCustomAttributes(
                    typeof(Microsoft.AspNetCore.Mvc.HttpPutAttribute),
                    inherit: true
                )
            )
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void El_controlador_expone_exactamente_cinco_acciones_HTTP()
    {
        var actions = typeof(CompanyFinancialDestinationController)
            .GetMethods(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
            )
            .Where(m => m.DeclaringType == typeof(CompanyFinancialDestinationController))
            .ToList();

        actions.Should().HaveCount(5);
        actions
            .Select(m => m.Name)
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    nameof(CompanyFinancialDestinationController.GetList),
                    nameof(CompanyFinancialDestinationController.Create),
                    nameof(CompanyFinancialDestinationController.Rename),
                    nameof(CompanyFinancialDestinationController.ChangeAccountingAccount),
                    nameof(CompanyFinancialDestinationController.SetActive),
                }
            );
    }

    // ── GetList (Fase 13 Remediación 01) ─────────────────────────────────

    [Fact]
    public void GetList_exige_perm_settings_financial_destinations_view()
    {
        var method = typeof(CompanyFinancialDestinationController).GetMethod(
            nameof(CompanyFinancialDestinationController.GetList)
        )!;
        var attr = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{SettingsPermissions.FinancialDestinationsView}");
    }

    [Fact]
    public async Task GetList_exitoso_retorna_200_y_envia_el_filtro_isActive_recibido()
    {
        object? sent = null;
        var controller = BuildController(req =>
        {
            sent = req;
            return Result<IReadOnlyList<CompanyFinancialDestinationDto>>.Success(
                new List<CompanyFinancialDestinationDto> { SampleDto(Guid.NewGuid()) }
            );
        });

        var response = await controller.GetList(true, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sent.Should().BeOfType<GetCompanyFinancialDestinationListQuery>();
        ((GetCompanyFinancialDestinationListQuery)sent!).IsActive.Should().BeTrue();
    }
}
