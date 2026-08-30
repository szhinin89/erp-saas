using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.DocTypes.Services;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Payables.UseCases;
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
using ERP.Domain.Modules.DocTypes.Enums;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Expenses;

/// <summary>
/// EXPENSES-WORKFLOW-INTEGRATION-01: <see cref="CreateConfirmedExpenseHandler"/> crea un gasto
/// directamente en Confirmado (sin borrador previo). Cubre los escenarios de política que
/// <see cref="IDocumentFlowPolicyService.EnsureDirectCreationAllowedAsync"/> puede resolver para
/// GASDOC — CreationMode.DirectCreation lo permite, DraftRequired lo bloquea (el gasto debe pasar
/// por borrador primero).
/// </summary>
public sealed class CreateConfirmedExpenseUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Crear_confirmado_directo_permitido_cuando_politica_GASDOC_no_lo_bloquea()
    {
        // WorkflowPolicy ya esta configurada en el Fixture para no bloquear por defecto
        // (CreationMode.DirectCreation).
        var fx = new Fixture();

        var result = await fx.Handler.Handle(fx.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Confirmed);
        fx.Docs.Verify(r => r.AddAsync(It.IsAny<ExpenseDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Crear_confirmado_directo_bloqueado_cuando_politica_GASDOC_es_DraftRequired()
    {
        var fx = new Fixture();
        fx.WorkflowPolicy
            .Setup(w =>
                w.EnsureDirectCreationAllowedAsync(
                    CompanyId,
                    DocTypeCodes.ExpenseDocument,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(DocumentFlowPolicyViolationException.DraftRequired(DocTypeCodes.ExpenseDocument));

        var result = await fx.Handler.Handle(fx.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should()
            .Be("La política de la empresa requiere guardar el gasto como borrador antes de confirmarlo.");
        fx.Docs.Verify(r => r.AddAsync(It.IsAny<ExpenseDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_confirmado_directo_crea_CxP_generica_con_OriginType_ExpenseDocument()
    {
        var fx = new Fixture();

        var result = await fx.Handler.Handle(fx.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fx.Payables.Verify(
            p =>
                p.CreateFromOriginAsync(
                    It.Is<CreateAccountsPayableFromOriginRequest>(req =>
                        req.OriginType == AccountsPayableOriginType.ExpenseDocument
                        && req.SupplierId == fx.Supplier.Id
                    ),
                    UserId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
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
        public Mock<IAccountsPayableService> Payables { get; } = new();
        public Mock<IDocumentFlowPolicyService> WorkflowPolicy { get; } = new();

        public Account Account { get; }
        public ExpenseCategoryNode Type { get; }
        public ExpenseCategoryNode Category { get; }
        public ExpenseCategoryNode Subcategory { get; }
        public PaymentTerm PaymentTerm { get; }
        public BusinessPartner Supplier { get; }
        public BusinessPartnerRole SupplierRole { get; }

        public CreateConfirmedExpenseHandler Handler =>
            new(
                Docs.Object,
                CategoryRepo.Object,
                Accounts.Object,
                Partners.Object,
                Roles.Object,
                PaymentTerms.Object,
                Tax.Object,
                Payables.Object,
                WorkflowPolicy.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId),
                NullLogger<CreateConfirmedExpenseHandler>.Instance
            );

        public Fixture()
        {
            Account = Account.Create(
                TenantId,
                CompanyId,
                AccountCode.Create("6.1.01.001"),
                "Gasto administrativo",
                null,
                AccountType.Expense,
                AccountNature.Debit,
                allowsPosting: true,
                UserId
            );
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
            Supplier = BusinessPartner.Create(TenantId, "04", "1791352688001", 2, "Proveedor Demo", UserId);
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
                .Setup(r => r.GetByTypeAsync(Supplier.Id, RoleType.Supplier, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SupplierRole);
            PaymentTerms
                .Setup(r => r.GetByIdAsync(TenantId, PaymentTerm.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PaymentTerm);
            CategoryRepo
                .Setup(r => r.GetByIdAsync(TenantId, Subcategory.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Subcategory);
            Accounts
                .Setup(r =>
                    r.GetByIdAsync(TenantId, CompanyId, Account.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(Account);
            Tax.Setup(t => t.GetVatRateWithNameAsync("0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxRateResult(0m, "IVA 0%"));
            Docs
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Payables
                .Setup(p =>
                    p.CreateFromOriginAsync(
                        It.IsAny<CreateAccountsPayableFromOriginRequest>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    (CreateAccountsPayableFromOriginRequest req, Guid createdBy, CancellationToken _) =>
                    {
                        var payable = AccountsPayable.CreateFromOrigin(
                            req.TenantId,
                            req.CompanyId,
                            req.BranchId,
                            req.SupplierId,
                            req.OriginType,
                            req.OriginId,
                            req.DocumentType,
                            req.DocumentNumber,
                            req.IssueDate,
                            req.AccountingDate,
                            createdBy
                        );
                        foreach (var installment in req.Installments)
                            payable.AddInstallment(
                                installment.InstallmentNumber,
                                installment.DueDate,
                                installment.Amount
                            );
                        return payable;
                    }
                );
            WorkflowPolicy
                .Setup(w =>
                    w.EnsureDirectCreationAllowedAsync(
                        CompanyId,
                        DocTypeCodes.ExpenseDocument,
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(Task.CompletedTask);
            WorkflowPolicy
                .Setup(w =>
                    w.GetRequiredAsync(CompanyId, DocTypeCodes.ExpenseDocument, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(
                    new DocumentFlowPolicyResult(
                        DocTypeCodes.ExpenseDocument,
                        IsActive: true,
                        CreationMode.DirectCreation,
                        ConfirmationMode.AutoConfirmOnCreate,
                        AuthorizationMode.None,
                        PendingDocumentMode.None,
                        CancellationMode.AllowedAfterConfirmationWithReversal,
                        RequiresCancellationReason: true,
                        RequiresAttachment: false,
                        RequiresSupplier: true,
                        RequiresDueDate: true,
                        PayableGenerationMode.OnConfirmation,
                        AccountingPostingMode.OnConfirmation,
                        InventoryImpactMode.None,
                        NotificationMode.None
                    )
                );
        }

        public CreateConfirmedExpenseCommand ValidCommand() =>
            new(
                Supplier.Id,
                new DateOnly(2026, 8, 27),
                new DateOnly(2026, 8, 27),
                "01",
                "001-001-000000123",
                PaymentTerm.Id,
                null,
                new[] { new ExpenseDraftLineRequest(Subcategory.Id, "Gasto", 1m, 10m) },
                Notes: "Confirmado directo"
            );
    }
}
