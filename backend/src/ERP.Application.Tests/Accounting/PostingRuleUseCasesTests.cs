using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.UseCases.PostingRules;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-POSTING-RULES-AUDIT-03 / ACCOUNTING-11 — validación de configuración al crear/editar
/// PostingRule: toda cuenta de Lines debe existir en la Company, estar activa y admitir movimiento;
/// la forma de las líneas (cantidad, Debe/Haber, AmountKind) la define PostingFactCatalog.
/// </summary>
public sealed class PostingRuleUseCasesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static Account PostableAccount(string code) =>
        Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create(code),
            "Cuenta de prueba",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            CreatedBy
        );

    private sealed class Mocks
    {
        public Mock<IPostingRuleRepository> PostingRules { get; } = new();
        public Mock<IAccountRepository> Accounts { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Mocks()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(CreatedBy);
            PostingRules
                .Setup(r =>
                    r.FindByKeyAsync(
                        TenantId,
                        CompanyId,
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((PostingRule?)null);
            // ACCOUNTING-POSTING-RULES-UI-12: Create/Update/Enable/Disable ahora resuelven
            // Account*/AccountIsActive/AccountAllowsPosting para el PostingRuleDto de respuesta
            // (GetByCompanyAsync) — sin este default, ToDictionary sobre `null` lanza en cada
            // handler que no registró explícitamente una cuenta. RegisterAccount agrega a la
            // misma lista respaldada por _registeredAccounts.
            Accounts
                .Setup(r => r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _registeredAccounts.AsReadOnly());
        }

        private readonly List<Account> _registeredAccounts = new();

        public void RegisterAccount(Account account)
        {
            Accounts
                .Setup(r =>
                    r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(account);
            _registeredAccounts.Add(account);
        }

        public CreatePostingRuleHandler BuildCreateHandler() =>
            new(
                PostingRules.Object,
                Accounts.Object,
                Tenant.Object,
                Company.Object,
                User.Object,
                Mock.Of<IDatabaseExceptionTranslator>()
            );

        public UpdatePostingRuleHandler BuildUpdateHandler() =>
            new(PostingRules.Object, Accounts.Object, Tenant.Object, Company.Object, User.Object);
    }

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_sin_Lines_es_rechazado_por_lineas_no_efectivas()
    {
        var m = new Mocks();
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreatePostingRuleCommand("Sales", "InvoiceIssued", []),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.PostingRules.Verify(
            r => r.AddAsync(It.IsAny<PostingRule>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Create_con_una_sola_linea_es_rechazado_por_lineas_no_efectivas()
    {
        var m = new Mocks();
        var account = PostableAccount("1.1.01");
        m.RegisterAccount(account);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreatePostingRuleCommand(
                "Sales",
                "InvoiceIssued",
                new[]
                {
                    new PostingRuleLineInput(
                        account.Id,
                        AccountNature.Debit,
                        PostingAmountKind.PendingBalance
                    ),
                }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.PostingRules.Verify(
            r => r.AddAsync(It.IsAny<PostingRule>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Create_con_cuenta_inactiva_en_Lines_es_rechazado()
    {
        var m = new Mocks();
        var inactive = PostableAccount("1.1.01");
        inactive.Disable(CreatedBy);
        var active = PostableAccount("4.1.01");
        m.RegisterAccount(inactive);
        m.RegisterAccount(active);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreatePostingRuleCommand(
                "Sales",
                "InvoiceIssued",
                new[]
                {
                    new PostingRuleLineInput(
                        inactive.Id,
                        AccountNature.Debit,
                        PostingAmountKind.PendingBalance
                    ),
                    new PostingRuleLineInput(
                        active.Id,
                        AccountNature.Credit,
                        PostingAmountKind.Subtotal
                    ),
                }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.PostingRules.Verify(
            r => r.AddAsync(It.IsAny<PostingRule>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Create_con_cuenta_AllowsPosting_false_en_Lines_es_rechazado()
    {
        var m = new Mocks();
        var summary = Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create("1.1"),
            "Activo Corriente (resumen)",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: false,
            CreatedBy
        );
        var active = PostableAccount("4.1.01");
        m.RegisterAccount(summary);
        m.RegisterAccount(active);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreatePostingRuleCommand(
                "Sales",
                "InvoiceIssued",
                new[]
                {
                    new PostingRuleLineInput(
                        summary.Id,
                        AccountNature.Debit,
                        PostingAmountKind.PendingBalance
                    ),
                    new PostingRuleLineInput(
                        active.Id,
                        AccountNature.Credit,
                        PostingAmountKind.Subtotal
                    ),
                }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.PostingRules.Verify(
            r => r.AddAsync(It.IsAny<PostingRule>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Create_con_cuenta_inexistente_en_Lines_es_rechazado()
    {
        var m = new Mocks();
        var active = PostableAccount("4.1.01");
        m.RegisterAccount(active);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreatePostingRuleCommand(
                "Sales",
                "InvoiceIssued",
                new[]
                {
                    new PostingRuleLineInput(
                        Guid.NewGuid(),
                        AccountNature.Debit,
                        PostingAmountKind.PendingBalance
                    ),
                    new PostingRuleLineInput(
                        active.Id,
                        AccountNature.Credit,
                        PostingAmountKind.Subtotal
                    ),
                }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        m.PostingRules.Verify(
            r => r.AddAsync(It.IsAny<PostingRule>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Create_con_2_lineas_validas_y_cuentas_activas_persiste_la_regla()
    {
        var m = new Mocks();
        var debit = PostableAccount("1.1.01");
        var credit = PostableAccount("4.1.01");
        m.RegisterAccount(debit);
        m.RegisterAccount(credit);
        var handler = m.BuildCreateHandler();

        var result = await handler.Handle(
            new CreatePostingRuleCommand(
                "Sales",
                "InvoiceIssued",
                new[]
                {
                    new PostingRuleLineInput(
                        debit.Id,
                        AccountNature.Debit,
                        PostingAmountKind.PendingBalance
                    ),
                    new PostingRuleLineInput(
                        credit.Id,
                        AccountNature.Credit,
                        PostingAmountKind.Subtotal
                    ),
                }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Lines.Should().HaveCount(2);
        m.PostingRules.Verify(
            r => r.AddAsync(It.IsAny<PostingRule>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // ── Update (ACCOUNTING-11) ───────────────────────────────────────────────

    [Fact]
    public async Task A11_Update_reemplaza_Lines_y_valida_forma_del_catalogo_y_cuentas()
    {
        var cash = PostableAccount("1.1.01");
        var receivable = PostableAccount("1.1.03");
        var revenue = PostableAccount("4.1.01");
        var otherCompany = Account.Create(
            TenantId,
            Guid.NewGuid(),
            AccountCode.Create("4.9.01"),
            "Otra empresa",
            null,
            AccountType.Income,
            AccountNature.Credit,
            allowsPosting: true,
            CreatedBy
        );
        var rule = PostingRule.Create(TenantId, CompanyId, "Sales", "InvoiceIssued", CreatedBy);
        rule.AddLine(receivable.Id, AccountNature.Debit, PostingAmountKind.PendingBalance);
        rule.AddLine(revenue.Id, AccountNature.Credit, PostingAmountKind.Subtotal);

        var m = new Mocks();
        m.PostingRules.Setup(r => r.GetByIdAsync(TenantId, CompanyId, rule.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);
        m.RegisterAccount(cash);
        m.RegisterAccount(receivable);
        m.RegisterAccount(revenue);
        var handler = m.BuildUpdateHandler();
        PostingRuleLineInput L(Account a, AccountNature n, PostingAmountKind k) => new(a.Id, n, k);

        var invalid = new (string Because, PostingRuleLineInput[] Lines)[]
        {
            ("una sola línea", [L(revenue, AccountNature.Credit, PostingAmountKind.Subtotal)]),
            ("sin Debe", [L(revenue, AccountNature.Credit, PostingAmountKind.Subtotal), L(cash, AccountNature.Credit, PostingAmountKind.TaxVat)]),
            ("sin Haber", [L(cash, AccountNature.Debit, PostingAmountKind.CashApplied), L(receivable, AccountNature.Debit, PostingAmountKind.PendingBalance)]),
            ("AmountKind fuera del catálogo del hecho", [L(cash, AccountNature.Debit, PostingAmountKind.HistoricalCost), L(revenue, AccountNature.Credit, PostingAmountKind.Subtotal)]),
            ("cuenta de otra empresa", [L(cash, AccountNature.Debit, PostingAmountKind.CashApplied), L(otherCompany, AccountNature.Credit, PostingAmountKind.Subtotal)]),
        };
        foreach (var (because, lines) in invalid)
        {
            var rejected = await handler.Handle(new UpdatePostingRuleCommand(rule.Id, lines), CancellationToken.None);
            rejected.IsSuccess.Should().BeFalse(because);
        }
        rule.Lines.Select(l => l.AccountId).Should().Equal([receivable.Id, revenue.Id], "una validación fallida no muta la regla");
        m.PostingRules.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

        var result = await handler.Handle(
            new UpdatePostingRuleCommand(
                rule.Id,
                [
                    L(cash, AccountNature.Debit, PostingAmountKind.CashApplied),
                    L(receivable, AccountNature.Debit, PostingAmountKind.PendingBalance),
                    L(revenue, AccountNature.Credit, PostingAmountKind.Subtotal),
                ]
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        rule.Lines.OrderBy(l => l.SortOrder)
            .Select(l => (l.AccountId, l.Nature, l.AmountKind))
            .Should()
            .Equal(
                (cash.Id, AccountNature.Debit, PostingAmountKind.CashApplied),
                (receivable.Id, AccountNature.Debit, PostingAmountKind.PendingBalance),
                (revenue.Id, AccountNature.Credit, PostingAmountKind.Subtotal)
            );
        m.PostingRules.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        var unsupported = await m.BuildCreateHandler()
            .Handle(new CreatePostingRuleCommand("Sales", "FreeText", [L(cash, AccountNature.Debit, PostingAmountKind.CashApplied), L(revenue, AccountNature.Credit, PostingAmountKind.Subtotal)]), CancellationToken.None);
        unsupported.IsSuccess.Should().BeFalse("solo claves del catálogo");
    }

    // ── Get (ACCOUNTING-POSTING-RULES-UI-12) ──────────────────────────────────

    [Fact]
    public async Task GetPostingRules_resuelve_datos_de_cuenta_por_cada_linea()
    {
        var debit = PostableAccount("1.1.03.001");
        var credit = PostableAccount("4.1.01.001");
        var rule = PostingRule.Create(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            CreatedBy
        );
        rule.AddLine(debit.Id, AccountNature.Debit, PostingAmountKind.PendingBalance);
        rule.AddLine(credit.Id, AccountNature.Credit, PostingAmountKind.Subtotal);

        var m = new Mocks();
        m.RegisterAccount(debit);
        m.RegisterAccount(credit);
        m.PostingRules.Setup(r =>
                r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { rule });

        var handler = new GetPostingRulesHandler(
            m.PostingRules.Object,
            m.Accounts.Object,
            m.Tenant.Object,
            m.Company.Object
        );

        var result = await handler.Handle(new GetPostingRulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!.Single();
        dto.Lines.Should().HaveCount(2);
        dto.Lines.Should()
            .Contain(l =>
                l.AccountId == debit.Id
                && l.AccountCode == "1.1.03.001"
                && l.AccountType == "Asset"
                && l.AccountNature == "Debit"
                && l.AccountIsActive
                && l.AccountAllowsPosting
            );
        dto.Lines.Should().Contain(l => l.AccountId == credit.Id && l.AccountCode == "4.1.01.001");
    }

    [Fact]
    public async Task GetPostingRules_linea_con_cuenta_inactiva_refleja_AccountIsActive_false()
    {
        var debit = PostableAccount("1.1.03.001");
        var credit = PostableAccount("4.1.01.001");
        credit.Disable(CreatedBy);
        var rule = PostingRule.Create(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            CreatedBy
        );
        rule.AddLine(debit.Id, AccountNature.Debit, PostingAmountKind.PendingBalance);
        rule.AddLine(credit.Id, AccountNature.Credit, PostingAmountKind.Subtotal);

        var m = new Mocks();
        m.RegisterAccount(debit);
        m.RegisterAccount(credit);
        m.PostingRules.Setup(r =>
                r.GetByCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { rule });

        var handler = new GetPostingRulesHandler(
            m.PostingRules.Object,
            m.Accounts.Object,
            m.Tenant.Object,
            m.Company.Object
        );

        var result = await handler.Handle(new GetPostingRulesQuery(), CancellationToken.None);

        result
            .Value!.Single()
            .Lines.Should()
            .Contain(l => l.AccountId == credit.Id && !l.AccountIsActive);
    }

    [Fact]
    public async Task GetPostingRuleById_resuelve_datos_de_cuenta()
    {
        var debit = PostableAccount("1.1.03.001");
        var credit = PostableAccount("4.1.01.001");
        var rule = PostingRule.Create(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            CreatedBy
        );
        rule.AddLine(debit.Id, AccountNature.Debit, PostingAmountKind.PendingBalance);
        rule.AddLine(credit.Id, AccountNature.Credit, PostingAmountKind.Subtotal);

        var m = new Mocks();
        m.RegisterAccount(debit);
        m.RegisterAccount(credit);
        m.PostingRules.Setup(r =>
                r.GetByIdAsync(TenantId, CompanyId, rule.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(rule);

        var handler = new GetPostingRuleByIdHandler(
            m.PostingRules.Object,
            m.Accounts.Object,
            m.Tenant.Object,
            m.Company.Object
        );

        var result = await handler.Handle(
            new GetPostingRuleByIdQuery(rule.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value!.Lines.Should()
            .Contain(l => l.AccountId == debit.Id && l.AccountCode == "1.1.03.001");
    }
}
