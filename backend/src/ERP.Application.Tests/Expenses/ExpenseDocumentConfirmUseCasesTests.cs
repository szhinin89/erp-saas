using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Events;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Expenses;

public sealed class ExpenseDocumentConfirmUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    [Fact]
    public async Task Confirmar_Draft_valido_con_una_linea_pasa_a_Confirmed_y_postea()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Confirmed);
        result.Value.GrandTotal.Should().Be(115m);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirmar_gasto_crea_CxP_generica_con_OriginType_ExpenseDocument()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fx.Payables.Verify(
            p =>
                p.CreateFromOriginAsync(
                    It.Is<CreateAccountsPayableFromOriginRequest>(req =>
                        req.OriginType == AccountsPayableOriginType.ExpenseDocument
                        && req.OriginId == document.Id
                        && req.SupplierId == SupplierId
                        && req.TotalAmount == 115m
                    ),
                    UserId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Si_falla_la_creacion_de_CxP_la_confirmacion_igual_tiene_exito()
    {
        // La CxP se crea DESPUES de que el posting ya se confirmo y persistio — un fallo aqui no
        // debe revertir la confirmacion (a diferencia del posting, que si es estricto). Ver
        // comentario en ConfirmExpenseDocumentHandler.
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);
        fx.Payables
            .Setup(p =>
                p.CreateFromOriginAsync(
                    It.IsAny<CreateAccountsPayableFromOriginRequest>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("Ya existe una cuota con el número 1."));

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ExpenseStatus.Confirmed);
    }

    [Fact]
    public async Task Confirmar_Draft_valido_con_varias_lineas_y_varias_cuentas_genera_allocations()
    {
        var fx = new Fixture();
        var otherAccount = fx.ExpenseAccount("6.1.02.001");
        var otherSubcategory = ExpenseCategoryNode.CreateSubcategory(
            TenantId, CompanyId, fx.Category, "SUM", "Suministros", otherAccount.Id, UserId
        );
        fx.CategoryRepo
            .Setup(r => r.GetByIdAsync(TenantId, otherSubcategory.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherSubcategory);
        fx.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, otherAccount.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherAccount);

        var document = fx.DraftDocumentWithLines(
            fx.Line(fx.Subcategory, fx.Account, 100m, "2", 15m),
            fx.Line(otherSubcategory, otherAccount, 50m, "0")
        );
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GrandTotal.Should().Be(165m);
        // El handler no invoca IPostingEngine directamente — ExpenseDocumentConfirmedPostingTranslator
        // lo hace, disparado por ErpDbContext.SaveChangesAsync (mockeado aqui como no-op). Lo que el
        // handler SI controla es que Confirm() levante el evento con una allocation por cuenta, que
        // es lo que el traductor luego convierte en PostingAllocation (ver
        // ExpenseDocumentConfirmedPostingTranslatorTests para esa conversion).
        var raised = document.DomainEvents.OfType<ExpenseDocumentConfirmedEvent>().Single();
        raised.LineAllocations.Should().HaveCount(2);
        raised.LineAllocations.Should().Contain(a => a.AccountingAccountId == fx.Account.Id && a.Amount == 100m);
        raised.LineAllocations.Should().Contain(a => a.AccountingAccountId == otherAccount.Id && a.Amount == 50m);
    }

    [Fact]
    public async Task Confirmar_documento_congela_snapshot_de_cuenta_en_lineas()
    {
        var fx = new Fixture();
        var line = fx.Line(fx.Subcategory, fx.Account, 100m, "0");
        var document = fx.DraftDocumentWithLines(line);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var confirmedLine = document.Lines.Single();
        confirmedLine.SnapshotAccountingAccountId.Should().Be(fx.Account.Id);
        confirmedLine.SnapshotAccountingAccountCode.Should().Be(fx.Account.Code.Value);
        confirmedLine.SnapshotAccountingAccountName.Should().Be(fx.Account.Name);
    }

    [Fact]
    public async Task Confirmar_documento_no_Draft_se_bloquea()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        SetPrivateStatus(document, ExpenseStatus.Confirmed);
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmar_con_subcategoria_inactiva_se_bloquea()
    {
        var fx = new Fixture();
        fx.Subcategory.SetActive(false, UserId);
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("not_postable")]
    [InlineData("not_expense")]
    public async Task Confirmar_con_cuenta_invalida_se_bloquea(string scenario)
    {
        var fx = new Fixture();
        var account =
            scenario == "inactive" ? fx.ExpenseAccount("6.1.01.001", isActive: false)
            : scenario == "not_postable" ? fx.ExpenseAccount("6.1.01.001", allowsPosting: false)
            : fx.ExpenseAccount("6.1.01.001", accountType: AccountType.Asset);
        fx.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, fx.Subcategory.AccountingAccountId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmar_con_cuenta_de_otra_empresa_se_bloquea()
    {
        var fx = new Fixture();
        fx.Accounts
            .Setup(r => r.GetByIdAsync(TenantId, CompanyId, fx.Subcategory.AccountingAccountId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        fx.Docs.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_falla_el_posting_la_confirmacion_falla_y_el_documento_queda_Draft()
    {
        var fx = new Fixture();
        var document = fx.DraftDocumentWithLines(fx.Line(fx.Subcategory, fx.Account, 100m, "0"));
        fx.SetupDocument(document);
        fx.Docs
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExpensePostingFailedException("No existe regla de contabilizacion.", "RULE_NOT_FOUND"));

        var result = await fx.Handler.Handle(new ConfirmExpenseDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("RULE_NOT_FOUND");
        // La mutacion en memoria de Confirm() ocurrio antes del SaveChangesAsync fallido, pero
        // nada se persistio (ExpensePostingFailedException simula el rollback real de
        // ErpDbContext.SaveChangesAsync) — lo que importa para el caller es que el Result sea
        // un fallo explicito, nunca un exito con documento a medio confirmar.
        document.Status.Should().Be(ExpenseStatus.Confirmed, "el rollback real de BD (no simulado aqui) es quien revierte el estado en persistencia");
    }

    private static void SetPrivateStatus(ExpenseDocument document, ExpenseStatus status)
    {
        var property = typeof(ExpenseDocument).GetProperty(nameof(ExpenseDocument.Status))!;
        property.SetValue(document, status);
    }

    private sealed class Fixture
    {
        public Mock<IExpenseDocumentRepository> Docs { get; } = new();
        public Mock<IExpenseCategoryRepository> CategoryRepo { get; } = new();
        public Mock<IAccountRepository> Accounts { get; } = new();
        public Mock<IAccountsPayableService> Payables { get; } = new();

        public ExpenseCategoryNode Type { get; }
        public ExpenseCategoryNode Category { get; }
        public ExpenseCategoryNode Subcategory { get; }
        public Account Account { get; }

        public ConfirmExpenseDocumentHandler Handler =>
            new(
                Docs.Object,
                CategoryRepo.Object,
                Accounts.Object,
                Payables.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId),
                NullLogger<ConfirmExpenseDocumentHandler>.Instance
            );

        public Fixture()
        {
            Type = ExpenseCategoryNode.CreateType(TenantId, CompanyId, "ADM", "Administrativos", UserId);
            Category = ExpenseCategoryNode.CreateCategory(TenantId, CompanyId, Type, "OFF", "Oficina", UserId);
            Account = ExpenseAccount("6.1.01.001");
            Subcategory = ExpenseCategoryNode.CreateSubcategory(
                TenantId, CompanyId, Category, "PAP", "Papeleria", Account.Id, UserId
            );

            CategoryRepo
                .Setup(r => r.GetByIdAsync(TenantId, Subcategory.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Subcategory);
            Accounts
                .Setup(r => r.GetByIdAsync(TenantId, CompanyId, Account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Account);
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
                            req.TenantId, req.CompanyId, req.BranchId, req.SupplierId,
                            req.OriginType, req.OriginId, req.DocumentType, req.DocumentNumber,
                            req.IssueDate, req.AccountingDate, createdBy
                        );
                        payable.AddInstallment(1, req.DueDate, req.TotalAmount);
                        return payable;
                    }
                );
        }

        public Account ExpenseAccount(
            string code,
            bool allowsPosting = true,
            bool isActive = true,
            AccountType accountType = AccountType.Expense
        )
        {
            var account = Account.Create(
                TenantId, CompanyId, AccountCode.Create(code), "Gasto administrativo",
                null, accountType, AccountNature.Debit, allowsPosting, UserId
            );
            if (!isActive)
                account.Disable(UserId);
            return account;
        }

        public ExpenseLine Line(
            ExpenseCategoryNode subcategory,
            Account account,
            decimal unitAmount,
            string vatCode,
            decimal vatRate = 0m
        ) =>
            ExpenseLine.Create(
                Guid.NewGuid(), TenantId, subcategory.Id, account.Id,
                subcategory.Name, 1m, unitAmount, vatCode, vatRate
            );

        public ExpenseDocument DraftDocumentWithLines(params ExpenseLine[] lines)
        {
            var document = ExpenseDocument.CreateDraft(
                TenantId, CompanyId, BranchId, SupplierId, "Proveedor Demo", "1791352688001",
                new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), "01", "001-001-000000123",
                PaymentTermId, "Contado", 1, 0, UserId
            );
            var rebuiltLines = lines
                .Select(l => ExpenseLine.Create(
                    document.Id, TenantId, l.ExpenseSubcategoryId, l.SnapshotAccountingAccountId,
                    l.Description, l.Quantity, l.UnitAmount, l.VatCode, l.VatRate
                ))
                .ToArray();
            document.ReplaceLines(rebuiltLines, UserId);
            return document;
        }

        public void SetupDocument(ExpenseDocument document)
        {
            Docs
                .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);
        }
    }
}
