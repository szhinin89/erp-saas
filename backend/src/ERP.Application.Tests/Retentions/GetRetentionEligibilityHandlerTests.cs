using ERP.Application.Common;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Retentions.UseCases;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Retentions.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-ELIGIBILITY-01 — cubre el query/handler que arma <see cref="RetentionEligibilityDto"/>
/// para <c>SourceDocumentType.ExpenseDocument</c>, incluyendo los casos de "no soportado en esta
/// fase" (PurchaseInvoice/Manual) y el fail-closed multi-tenant/branch. Solo lectura: el handler
/// no depende de IUnitOfWork ni de ningún repositorio de escritura — no hay side effects que
/// verificar además de la ausencia de llamadas a un mock de escritura (no existe ninguno aquí).
/// </summary>
public sealed class GetRetentionEligibilityHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid OtherBranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ExpenseSubcategoryId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();

    [Fact]
    public async Task PurchaseInvoice_devuelve_no_soportado_en_esta_fase_distinguible_de_no_elegible()
    {
        var fx = new Fixture();

        var result = await fx.Handler.Handle(
            new GetRetentionEligibilityQuery(RetentionSourceDocumentType.PurchaseInvoice, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSupportedInThisPhase.Should().BeFalse();
        result.Value.Reasons.Should().Contain(r => r.Contains("NotSupportedInThisPhase"));
        // Un "no soportado" nunca debe leerse como "no elegible por regla fiscal": todos los
        // campos de elegibilidad quedan en su valor neutro, nunca en true.
        result.Value.CanRetainVat.Should().BeFalse();
        result.Value.CanRetainIncome.Should().BeFalse();
        fx.EligibilityService.Verify(
            s => s.EvaluateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()
            ),
            Times.Never,
            "no debe evaluarse ninguna regla fiscal para un tipo de origen no soportado"
        );
    }

    [Fact]
    public async Task Manual_devuelve_no_soportado_en_esta_fase()
    {
        var fx = new Fixture();

        var result = await fx.Handler.Handle(
            new GetRetentionEligibilityQuery(RetentionSourceDocumentType.Manual, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSupportedInThisPhase.Should().BeFalse();
    }

    [Fact]
    public async Task ExpenseDocument_confirmado_de_la_sucursal_activa_evalua_elegibilidad()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(BranchId);
        fx.SetupDocument(document);
        fx.SetupEligibility(
            document.SupplierId,
            new RetentionEligibilityResult(
                CanRetainVat: true,
                CanRetainIncome: true,
                IsSupplierExempt: false,
                HasRetainableBase: true,
                MissingRetentionCode: false,
                IsSupplierRequiredToKeepAccounting: false,
                SuggestedVatRetentionCode: "725",
                SuggestedIncomeRetentionCode: "303",
                Reasons: Array.Empty<string>()
            )
        );

        var result = await fx.Handler.Handle(
            new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSupportedInThisPhase.Should().BeTrue();
        result.Value.CanRetainVat.Should().BeTrue();
        result.Value.CanRetainIncome.Should().BeTrue();
    }

    [Fact]
    public async Task ExpenseDocument_de_otra_sucursal_falla_cerrado_con_NotFound()
    {
        var fx = new Fixture();
        var document = fx.ConfirmedDocument(OtherBranchId);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.EligibilityService.Verify(
            s => s.EvaluateAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()
            ),
            Times.Never,
            "documento de otra sucursal nunca debe llegar a evaluarse — falla cerrado antes"
        );
    }

    [Fact]
    public async Task ExpenseDocument_inexistente_o_de_otro_tenant_devuelve_NotFound()
    {
        var fx = new Fixture();
        fx.Docs
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExpenseDocument?)null);

        var result = await fx.Handler.Handle(
            new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task ExpenseDocument_en_Draft_se_bloquea_con_error_de_validacion_no_excepcion()
    {
        var fx = new Fixture();
        var document = fx.DraftDocument(BranchId);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(
            new GetRetentionEligibilityQuery(RetentionSourceDocumentType.ExpenseDocument, document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("confirmados");
    }

    private sealed class Fixture
    {
        public Mock<IExpenseDocumentRepository> Docs { get; } = new();
        public Mock<IRetentionEligibilityService> EligibilityService { get; } = new();

        public GetRetentionEligibilityHandler Handler =>
            new(
                Docs.Object,
                EligibilityService.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId)
            );

        public void SetupDocument(ExpenseDocument document) =>
            Docs
                .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

        public void SetupEligibility(Guid supplierId, RetentionEligibilityResult result) =>
            EligibilityService
                .Setup(s => s.EvaluateAsync(
                    TenantId, CompanyId, supplierId,
                    It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(result);

        public ExpenseDocument DraftDocument(Guid branchId) =>
            ExpenseDocument.CreateDraft(
                TenantId, CompanyId, branchId, SupplierId, "Proveedor Demo", "1791352688001",
                new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), "01", "001-001-000000123",
                Guid.NewGuid(), "Contado", 1, 0, UserId
            );

        public ExpenseDocument ConfirmedDocument(Guid branchId)
        {
            var document = DraftDocument(branchId);
            var line = ExpenseLine.Create(
                document.Id, TenantId, ExpenseSubcategoryId, ExpenseAccountId,
                "Internet", 1m, 100m, "0"
            );
            document.ReplaceLines([line], UserId);
            document.Confirm(
                new Dictionary<Guid, (Guid, string?, string?)>
                {
                    [line.Id] = (ExpenseAccountId, "6.1.01", "Internet"),
                },
                UserId
            );
            return document;
        }
    }
}
