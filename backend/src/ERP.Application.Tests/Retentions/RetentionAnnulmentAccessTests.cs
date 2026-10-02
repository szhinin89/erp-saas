using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01/01B — los pasos de la anulación SRI se autorizan por el documento
/// ORIGEN, server-side, con el permiso de anular el origen (presentar, verificar en el SRI, desistir).
/// "Ya presenté la solicitud" dispara la verificación automática en ConsultaComprobante. No existe un
/// comando para declarar el estado fiscal (test 9). Sin acceso al origen, otra empresa u otra sucursal →
/// 404 sin tocar el servicio. Nunca basta un permiso de lectura.
/// </summary>
public sealed class RetentionAnnulmentAccessTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IRetentionAnnulmentRequestRepository> Requests { get; } = new();
        public Mock<IRetentionDocumentRepository> Retentions { get; } = new();
        public Mock<IRetentionAnnulmentService> Service { get; } = new();
        public RetentionAnnulmentRequest Request { get; }
        private readonly string[] _granted;
        private readonly Guid _branchId;

        public Fixture(RetentionSourceDocumentType sourceType, string[] granted, Guid? retentionBranch = null)
        {
            _granted = granted;
            _branchId = retentionBranch ?? BranchId;
            var retention = RetentionDocument.Create(
                TenantId, CompanyId, _branchId, sourceType, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UserId);
            retention.AddLine(RetentionDocumentLine.Create(retention.Id, TenantId, RetentionTaxType.Vat, "725", "IVA", 15m, 30m, 4.5m));
            retention.Issue("001-001-000000001", new DateOnly(2026, 9, 17), UserId);
            Request = RetentionAnnulmentRequest.Create(retention, Guid.NewGuid(), new string('1', 49), "1791", "Prov", "Motivo", UserId);

            Requests.Setup(r => r.GetByIdAsync(TenantId, CompanyId, Request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Request);
            Retentions.Setup(r => r.GetByIdAsync(TenantId, retention.Id, It.IsAny<CancellationToken>())).ReturnsAsync(retention);
            Service
                .Setup(s => s.MarkSubmittedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<RetentionAnnulmentRequest>.Success(Request));
            Service
                .Setup(s => s.VerifyWithSriAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<RetentionAnnulmentRequest>.Success(Request, ApiResponseCodes.Retentions.SriAnnulmentPending));
        }

        private RetentionAnnulmentAccess Access()
        {
            var authorizer = new Mock<IRuntimePermissionAuthorizer>();
            authorizer
                .Setup(a => a.IsAuthorizedAsync(It.IsAny<string>(), UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, Guid _, string _, CancellationToken _) => _granted.Contains(key));
            var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
            var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId && c.HasCompanyContext);
            var user = Mock.Of<ICurrentUser>(u => u.UserId == UserId);
            var clock = new Mock<ICompanyClock>();
            clock.Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(new DateOnly(2026, 10, 1));
            return new RetentionAnnulmentAccess(
                Requests.Object,
                Retentions.Object,
                new RetentionSourceAccess(authorizer.Object, user, tenant, company, Retentions.Object),
                tenant,
                company,
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId && b.HasBranchContext),
                clock.Object
            );
        }

        public Task<Result<ERP.Application.Modules.Retentions.DTOs.RetentionAnnulmentRequestDto>> SubmitAsync(Guid? requestId = null) =>
            new SubmitRetentionAnnulmentHandler(Access(), Service.Object, Mock.Of<ICurrentUser>(u => u.UserId == UserId))
                .Handle(new SubmitRetentionAnnulmentCommand(requestId ?? Request.Id, new DateOnly(2026, 9, 20), "T-1", null), CancellationToken.None);

        public Task<Result<ERP.Application.Modules.Retentions.DTOs.RetentionAnnulmentRequestDto>> VerifyAsync() =>
            new VerifyRetentionAnnulmentWithSriHandler(Access(), Service.Object, Mock.Of<ICurrentUser>(u => u.UserId == UserId))
                .Handle(new VerifyRetentionAnnulmentWithSriCommand(Request.Id), CancellationToken.None);

        public void VerifyServiceNeverCalled()
        {
            Service.Verify(s => s.MarkSubmittedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            Service.Verify(s => s.VerifyWithSriAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task Compra_con_permiso_de_anular_compra_presenta_y_se_verifica_en_el_SRI_automaticamente()
    {
        var fx = new Fixture(RetentionSourceDocumentType.PurchaseInvoice, [PurchasePermissions.View, PurchasePermissions.Update]);

        var result = await fx.SubmitAsync();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.SriAnnulmentPending, "el resultado es el de la consulta automática");
        result.Value!.OrdinaryDeadline.Should().Be(new DateOnly(2026, 10, 7));
        result.Value.IsPastOrdinaryDeadline.Should().BeFalse();
        fx.Service.Verify(s => s.VerifyWithSriAsync(TenantId, CompanyId, fx.Request.Id, UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_la_verificacion_no_se_aplica_la_presentacion_queda_registrada_y_se_informa()
    {
        var fx = new Fixture(RetentionSourceDocumentType.PurchaseInvoice, [PurchasePermissions.View, PurchasePermissions.Update]);
        fx.Service
            .Setup(s => s.VerifyWithSriAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RetentionAnnulmentRequest>.ValidationFailure("lock"));

        var result = await fx.SubmitAsync();

        result.IsSuccess.Should().BeTrue();
        result.Code.Should().Be(ApiResponseCodes.Retentions.SriVerificationFailed);
    }

    [Fact]
    public async Task Verificar_en_el_SRI_usa_el_permiso_de_anular_el_origen()
    {
        var fx = new Fixture(RetentionSourceDocumentType.ExpenseDocument, [ExpensePermissions.DocumentsView, ExpensePermissions.DocumentsCancel]);

        var result = await fx.VerifyAsync();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.SriAnnulmentPending);
    }

    [Fact]
    public void Test9_no_existe_un_comando_para_declarar_el_estado_fiscal()
    {
        var commands = typeof(VerifyRetentionAnnulmentWithSriCommand).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(VerifyRetentionAnnulmentWithSriCommand).Namespace && t.Name.EndsWith("Command", StringComparison.Ordinal))
            .ToList();

        commands.Should().NotBeEmpty();
        commands.Select(t => t.Name).Should().NotContain(n => n.Contains("Resolve", StringComparison.Ordinal));
        commands
            .SelectMany(t => t.GetProperties())
            .Where(p => p.PropertyType == typeof(SriFiscalStatus) || p.PropertyType == typeof(SriFiscalStatus?)
                || p.PropertyType == typeof(RetentionAnnulmentStatus) || p.PropertyType == typeof(RetentionAnnulmentStatus?))
            .Should().BeEmpty("el usuario nunca declara ANULADO: lo informa ConsultaComprobante");
        typeof(VerifyRetentionAnnulmentWithSriCommand).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(["RequestId"]);
    }

    [Fact]
    public async Task Solo_lectura_no_presenta_ni_verifica()
    {
        var fx = new Fixture(RetentionSourceDocumentType.PurchaseInvoice, [PurchasePermissions.View]);

        (await fx.SubmitAsync()).Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        (await fx.VerifyAsync()).Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        fx.VerifyServiceNeverCalled();
    }

    [Fact]
    public async Task Permisos_de_Gastos_no_dan_acceso_a_la_anulacion_de_una_compra()
    {
        var fx = new Fixture(
            RetentionSourceDocumentType.PurchaseInvoice,
            [ExpensePermissions.DocumentsView, ExpensePermissions.DocumentsCancel, ElectronicDocumentsPermissions.Retry]
        );

        (await fx.SubmitAsync()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await fx.VerifyAsync()).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.VerifyServiceNeverCalled();
    }

    [Fact]
    public async Task Otra_sucursal_u_otra_empresa_responde_404_sin_tocar_el_servicio()
    {
        var otherBranch = new Fixture(
            RetentionSourceDocumentType.PurchaseInvoice,
            [PurchasePermissions.View, PurchasePermissions.Update, ElectronicDocumentsPermissions.Retry],
            retentionBranch: Guid.NewGuid()
        );
        (await otherBranch.SubmitAsync()).Code.Should().Be(ApiResponseCodes.Common.NotFound);

        var otherCompany = new Fixture(
            RetentionSourceDocumentType.PurchaseInvoice,
            [PurchasePermissions.View, PurchasePermissions.Update, ElectronicDocumentsPermissions.Retry]
        );
        // El repositorio filtra por tenant + empresa activos: una solicitud ajena no se encuentra.
        (await otherCompany.SubmitAsync(Guid.NewGuid())).Code.Should().Be(ApiResponseCodes.Common.NotFound);

        otherBranch.VerifyServiceNeverCalled();
        otherCompany.VerifyServiceNeverCalled();
    }
}
