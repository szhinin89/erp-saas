using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.DocTypes.Services;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using FluentAssertions;
using Moq;
using System.Reflection;

namespace ERP.Application.Tests.Expenses;

public sealed class ExpenseDocumentDraftUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Create_Draft_valido_con_linea_de_subcategoria_activa_recalcula_totales()
    {
        var fx = new Fixture();
        var command = fx.ValidCreateCommand(
            new ExpenseDraftLineRequest(
                fx.Subcategory.Id,
                null,
                2m,
                100m,
                DiscountValue: 10m,
                VatCode: "2"
            )
        );

        var result = await fx.CreateHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Draft);
        result.Value.BranchId.Should().Be(BranchId);
        result.Value.Subtotal.Should().Be(200m);
        result.Value.TotalDiscount.Should().Be(10m);
        result.Value.TotalTax.Should().Be(28.50m);
        result.Value.GrandTotal.Should().Be(218.50m);
        result.Value.Lines.Single().Description.Should().Be(fx.Subcategory.Name);
        result.Value.Lines.Single().SnapshotAccountingAccountCode.Should().Be("6.1.01.001");
        fx.Docs.Verify(r => r.AddAsync(It.IsAny<ExpenseDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_Draft_permitido_cuando_politica_GASDOC_no_bloquea_borrador()
    {
        // EnsureDraftCreationAllowedAsync no lanza para CreationMode.DraftRequired (que es la
        // política inicial obligatoria de GASDOC) — solo CreationMode.DirectCreation bloquea la
        // creación de un borrador, cubierto en el test siguiente. WorkflowPolicy ya esta
        // configurada para no lanzar por defecto en el Fixture.
        var fx = new Fixture();

        var result = await fx.CreateHandler.Handle(fx.ValidCreateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Draft);
    }

    [Fact]
    public async Task Create_Draft_bloqueado_cuando_politica_GASDOC_es_DirectCreation()
    {
        var fx = new Fixture();
        fx.WorkflowPolicy
            .Setup(w =>
                w.EnsureDraftCreationAllowedAsync(
                    CompanyId,
                    DocTypeCodes.ExpenseDocument,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(DocumentFlowPolicyViolationException.DraftNotAllowed(DocTypeCodes.ExpenseDocument));

        var result = await fx.CreateHandler.Handle(fx.ValidCreateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should()
            .Be("La política de la empresa no permite guardar borradores para documentos de gasto.");
        fx.Docs.Verify(r => r.AddAsync(It.IsAny<ExpenseDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Create_sin_proveedor_se_bloquea_en_validator()
    {
        var fx = new Fixture();
        var command = fx.ValidCreateCommand().WithSupplier(Guid.Empty);

        var result = new CreateExpenseDraftValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateExpenseDraftCommand.SupplierId));
    }

    [Fact]
    public void Create_sin_lineas_se_bloquea_en_validator()
    {
        var fx = new Fixture();
        var command = fx.ValidCreateCommand(Array.Empty<ExpenseDraftLineRequest>());

        var result = new CreateExpenseDraftValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateExpenseDraftCommand.Lines));
    }

    [Fact]
    public async Task Create_con_nodo_que_no_es_Subcategory_se_bloquea()
    {
        var fx = new Fixture();
        fx.CategoryRepo
            .Setup(r => r.GetByIdAsync(TenantId, fx.Category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fx.Category);

        var result = await fx.CreateHandler.Handle(
            fx.ValidCreateCommand(new ExpenseDraftLineRequest(fx.Category.Id, "Oficina", 1m, 10m)),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Create_con_subcategoria_inactiva_se_bloquea()
    {
        var fx = new Fixture();
        fx.Subcategory.SetActive(false, UserId);

        var result = await fx.CreateHandler.Handle(fx.ValidCreateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Create_con_subcategoria_sin_cuenta_contable_se_bloquea()
    {
        var fx = new Fixture();
        SetPrivateProperty<Guid?>(fx.Subcategory, nameof(ExpenseCategoryNode.AccountingAccountId), null);

        var result = await fx.CreateHandler.Handle(fx.ValidCreateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("not_postable")]
    [InlineData("not_expense")]
    [InlineData("other_company")]
    public async Task Create_con_cuenta_no_utilizable_se_bloquea(string scenario)
    {
        var fx = new Fixture();
        var account =
            scenario == "inactive" ? fx.ExpenseAccount(isActive: false)
            : scenario == "not_postable" ? fx.ExpenseAccount(allowsPosting: false)
            : scenario == "not_expense" ? fx.ExpenseAccount(accountType: AccountType.Asset)
            : null;

        fx.SetupAccount(account);

        var result = await fx.CreateHandler.Handle(fx.ValidCreateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should()
            .Be(
                scenario == "other_company"
                    ? ApiResponseCodes.Common.NotFound
                    : ApiResponseCodes.Common.ValidationError
            );
    }

    [Fact]
    public async Task Update_de_documento_no_Draft_se_bloquea()
    {
        var fx = new Fixture();
        var document = fx.ExistingDraftDocument();
        SetPrivateProperty(document, nameof(ExpenseDocument.Status), ExpenseStatus.Confirmed);
        fx.Docs
            .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await fx.UpdateHandler.Handle(
            fx.ValidUpdateCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ExpenseLine_y_request_no_exponen_campos_de_inventario_o_compras()
    {
        var forbidden = new[]
        {
            "ItemId",
            "WarehouseId",
            "UoM",
            "UomCode",
            "Packaging",
            "PackagingLevelId",
            "ReceptionLineId",
            "PurchaseReceptionLineId",
            "Kardex",
            "Pvp",
            "PurchaseInvoiceDetailId",
        };

        var exposed = typeof(ExpenseLine)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Concat(
                typeof(ExpenseDraftLineRequest)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => p.Name)
            )
            .ToList();

        foreach (var forbiddenName in forbidden)
            exposed.Should().NotContain(forbiddenName);
    }

    private static void SetPrivateProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance
        );
        property!.SetValue(target, value);
    }

    private sealed class Fixture
    {
        public Mock<IExpenseDocumentRepository> Docs { get; } = new();
        public Mock<IExpenseCategoryRepository> CategoryRepo { get; } = new();
        public Mock<IAccountRepository> Accounts { get; } = new();
        public Mock<IBusinessPartnerRepository> Partners { get; } = new();
        public Mock<IBusinessPartnerRoleRepository> Roles { get; } = new();
        public Mock<IPaymentTermRepository> PaymentTerms { get; } = new();
        public Mock<ISriTaxResolver> Tax { get; } = new();
        public Mock<IDocumentFlowPolicyService> WorkflowPolicy { get; } = new();

        public Account Account { get; }
        public ExpenseCategoryNode Type { get; }
        public ExpenseCategoryNode Category { get; }
        public ExpenseCategoryNode Subcategory { get; }
        public PaymentTerm PaymentTerm { get; }
        public BusinessPartner Supplier { get; }
        public BusinessPartnerRole SupplierRole { get; }

        public CreateExpenseDraftHandler CreateHandler =>
            new(
                Docs.Object,
                CategoryRepo.Object,
                Accounts.Object,
                Partners.Object,
                Roles.Object,
                PaymentTerms.Object,
                Tax.Object,
                WorkflowPolicy.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId)
            );

        public UpdateExpenseDraftHandler UpdateHandler =>
            new(
                Docs.Object,
                CategoryRepo.Object,
                Accounts.Object,
                Partners.Object,
                Roles.Object,
                PaymentTerms.Object,
                Tax.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId)
            );

        public Fixture()
        {
            Account = ExpenseAccount();
            Type = ExpenseCategoryNode.CreateType(TenantId, CompanyId, "ADM", "Administrativos", UserId);
            Category = ExpenseCategoryNode.CreateCategory(TenantId, CompanyId, Type, "OFF", "Oficina", UserId);
            Subcategory = ExpenseCategoryNode.CreateSubcategory(
                TenantId,
                CompanyId,
                Category,
                "PAP",
                "Papeleria",
                Account.Id,
                UserId
            );
            PaymentTerm = PaymentTerm.Create(TenantId, "NET30", "Credito 30 dias", 1, 30, UserId);
            Supplier = BusinessPartner.Create(
                TenantId,
                "04",
                "1791352688001",
                2,
                "Proveedor Demo",
                UserId
            );
            SupplierRole = BusinessPartnerRole.Create(
                TenantId,
                Supplier.Id,
                RoleType.Supplier,
                UserId,
                SupplierRoleConfig.Create(PaymentTerm.Id)
            );

            Partners
                .Setup(r => r.GetByIdAsync(Supplier.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Supplier);
            Roles
                .Setup(r =>
                    r.GetByTypeAsync(Supplier.Id, RoleType.Supplier, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(SupplierRole);
            PaymentTerms
                .Setup(r => r.GetByIdAsync(TenantId, PaymentTerm.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PaymentTerm);
            CategoryRepo
                .Setup(r => r.GetByIdAsync(TenantId, Subcategory.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Subcategory);
            SetupAccount(Account);
            Tax.Setup(t => t.GetVatRateWithNameAsync("0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxRateResult(0m, "IVA 0%"));
            Tax.Setup(t => t.GetVatRateWithNameAsync("2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxRateResult(15m, "IVA 15%"));
            WorkflowPolicy
                .Setup(w =>
                    w.EnsureDraftCreationAllowedAsync(
                        CompanyId,
                        DocTypeCodes.ExpenseDocument,
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(Task.CompletedTask);
        }

        public Account ExpenseAccount(
            bool allowsPosting = true,
            bool isActive = true,
            AccountType accountType = AccountType.Expense
        )
        {
            var account = Account.Create(
                TenantId,
                CompanyId,
                AccountCode.Create("6.1.01.001"),
                "Gasto administrativo",
                null,
                accountType,
                AccountNature.Debit,
                allowsPosting,
                UserId
            );

            if (!isActive)
                account.Disable(UserId);

            return account;
        }

        public void SetupAccount(Account? account)
        {
            Accounts
                .Setup(r =>
                    r.GetByIdAsync(TenantId, CompanyId, Subcategory.AccountingAccountId!.Value, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(account);
        }

        public CreateExpenseDraftCommand ValidCreateCommand(
            params ExpenseDraftLineRequest[]? lines
        ) =>
            ValidCreateCommand((IReadOnlyList<ExpenseDraftLineRequest>?)lines);

        public CreateExpenseDraftCommand ValidCreateCommand(
            IReadOnlyList<ExpenseDraftLineRequest>? lines = null
        ) =>
            new(
                Supplier.Id,
                new DateOnly(2026, 8, 27),
                new DateOnly(2026, 8, 27),
                "01",
                "001-001-000000123",
                PaymentTerm.Id,
                null,
                lines ?? new[] { new ExpenseDraftLineRequest(Subcategory.Id, "Gasto", 1m, 10m) },
                Notes: "Borrador"
            );

        public UpdateExpenseDraftCommand ValidUpdateCommand(Guid id) =>
            new(
                id,
                Supplier.Id,
                new DateOnly(2026, 8, 27),
                new DateOnly(2026, 8, 27),
                "01",
                "001-001-000000123",
                PaymentTerm.Id,
                null,
                new[] { new ExpenseDraftLineRequest(Subcategory.Id, "Gasto", 1m, 10m) },
                Notes: "Borrador editado"
            );

        public ExpenseDocument ExistingDraftDocument()
        {
            var document = ExpenseDocument.CreateDraft(
                TenantId,
                CompanyId,
                BranchId,
                Supplier.Id,
                Supplier.Name.LegalName,
                Supplier.Identification.Number,
                new DateOnly(2026, 8, 27),
                new DateOnly(2026, 8, 27),
                "01",
                "001-001-000000123",
                PaymentTerm.Id,
                PaymentTerm.Name,
                PaymentTerm.Installments,
                PaymentTerm.DaysBetweenInstallments,
                UserId
            );
            return document;
        }
    }
}

file static class ExpenseDraftCommandExtensions
{
    public static CreateExpenseDraftCommand WithSupplier(
        this CreateExpenseDraftCommand command,
        Guid supplierId
    ) =>
        command with
        {
            SupplierId = supplierId,
        };
}
