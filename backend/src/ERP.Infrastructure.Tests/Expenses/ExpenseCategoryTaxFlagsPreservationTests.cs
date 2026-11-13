using ERP.Application.Common;
using ERP.Application.Modules.Expenses.UseCases.Categories;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Expenses;
using ERP.Infrastructure.Tests.Accounting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ERP.Infrastructure.Tests.Expenses;

/// <summary>
/// F-35 (API-DOC-V1): <c>PUT</c> de una categoría de gasto no debe modificar en silencio
/// <c>isDeductible</c> ni <c>requiresInvoice</c>. Cuando el comando no informa los indicadores se
/// conservan los valores actuales; los valores explícitos se aplican. PostgreSQL real
/// (fixture desechable compartido con Accounting).
/// </summary>
public sealed class ExpenseCategoryTaxFlagsPreservationTests : IClassFixture<AccountingManual08Fixture>
{
    private readonly AccountingManual08Fixture _fixture;
    public ExpenseCategoryTaxFlagsPreservationTests(AccountingManual08Fixture fixture) => _fixture = fixture;
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed record Seeded(
        ManualBook Book, Guid TypeId, Guid CategoryId, Guid SubId, Guid AccountId, Guid OtherAccountId);

    private async Task<Seeded> SeedAsync(bool deductible, bool requiresInvoice)
    {
        var book = await _fixture.BookAsync();
        await using var db = book.Db();
        Account Exp(string code) => Account.Create(book.TenantId, book.CompanyId, AccountCode.Create(code),
            "Gasto " + code, null, AccountType.Expense, AccountNature.Debit, true, book.UserId);
        var a1 = Exp("6.1.01");
        var a2 = Exp("6.1.02");
        db.Accounts.AddRange(a1, a2);
        var type = ExpenseCategoryNode.CreateType(book.TenantId, book.CompanyId, "T1", "Tipo", book.UserId);
        var cat = ExpenseCategoryNode.CreateCategory(book.TenantId, book.CompanyId, type, "C1", "Cat", book.UserId);
        var sub = ExpenseCategoryNode.CreateSubcategory(book.TenantId, book.CompanyId, cat, "S1", "Sub", a1.Id,
            book.UserId, null, deductible, requiresInvoice);
        db.ExpenseCategoryNodes.AddRange(type, cat, sub);
        await db.SaveChangesAsync();
        return new(book, type.Id, cat.Id, sub.Id, a1.Id, a2.Id);
    }

    private static UpdateExpenseCategoryNodeHandler Handler(ManualBook b, ErpDbContext db) =>
        new(new ExpenseCategoryRepository(db, Mock.Of<ICurrentCompany>(c => c.CompanyId == b.CompanyId)),
            new AccountRepository(db),
            Mock.Of<ICurrentTenant>(t => t.TenantId == b.TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == b.CompanyId),
            Mock.Of<ICurrentUser>(u => u.UserId == b.UserId));

    private static async Task<ExpenseCategoryNode> LoadAsync(Seeded s, Guid id)
    {
        await using var verify = s.Book.Db();
        return await verify.ExpenseCategoryNodes.AsNoTracking().SingleAsync(n => n.Id == id);
    }

    private static async Task UpdateAsync(Seeded s, UpdateExpenseCategoryNodeCommand cmd)
    {
        await using var db = s.Book.Db();
        (await Handler(s.Book, db).Handle(cmd, Ct)).IsSuccess.Should().BeTrue();
    }

    public static TheoryData<bool, bool> FlagCombinations => new()
    {
        { false, false }, { false, true }, { true, false }, { true, true },
    };

    [Theory]
    [MemberData(nameof(FlagCombinations))]
    public async Task Cambiar_nombre_conserva_los_indicadores(bool deductible, bool requiresInvoice)
    {
        var s = await SeedAsync(deductible, requiresInvoice);
        await UpdateAsync(s, new(s.SubId, "S1", "Nombre nuevo", s.AccountId));
        var saved = await LoadAsync(s, s.SubId);
        saved.Name.Should().Be("Nombre nuevo");
        saved.IsDeductible.Should().Be(deductible);
        saved.RequiresInvoice.Should().Be(requiresInvoice);
    }

    [Theory]
    [MemberData(nameof(FlagCombinations))]
    public async Task Cambiar_codigo_conserva_los_indicadores(bool deductible, bool requiresInvoice)
    {
        var s = await SeedAsync(deductible, requiresInvoice);
        await UpdateAsync(s, new(s.SubId, "S1X", "Sub", s.AccountId));
        var saved = await LoadAsync(s, s.SubId);
        saved.Code.Should().Be("S1X");
        saved.IsDeductible.Should().Be(deductible);
        saved.RequiresInvoice.Should().Be(requiresInvoice);
    }

    [Theory]
    [MemberData(nameof(FlagCombinations))]
    public async Task Cambiar_cuenta_contable_conserva_los_indicadores(bool deductible, bool requiresInvoice)
    {
        var s = await SeedAsync(deductible, requiresInvoice);
        await UpdateAsync(s, new(s.SubId, "S1", "Sub", s.OtherAccountId, "nota"));
        var saved = await LoadAsync(s, s.SubId);
        saved.AccountingAccountId.Should().Be(s.OtherAccountId);
        saved.Description.Should().Be("nota");
        saved.IsDeductible.Should().Be(deductible);
        saved.RequiresInvoice.Should().Be(requiresInvoice);
    }

    [Fact]
    public async Task Cada_indicador_explicito_se_aplica_de_forma_independiente()
    {
        var s = await SeedAsync(deductible: false, requiresInvoice: false);

        await UpdateAsync(s, new(s.SubId, "S1", "Sub", s.AccountId, null, IsDeductible: true));
        var afterFirst = await LoadAsync(s, s.SubId);
        afterFirst.IsDeductible.Should().BeTrue();
        afterFirst.RequiresInvoice.Should().BeFalse("no se informó requiresInvoice");

        await UpdateAsync(s, new(s.SubId, "S1", "Sub", s.AccountId, null, RequiresInvoice: true));
        var afterSecond = await LoadAsync(s, s.SubId);
        afterSecond.IsDeductible.Should().BeTrue("no se informó isDeductible");
        afterSecond.RequiresInvoice.Should().BeTrue();

        await UpdateAsync(s, new(s.SubId, "S1", "Sub", s.AccountId, null, false, false));
        var afterThird = await LoadAsync(s, s.SubId);
        afterThird.IsDeductible.Should().BeFalse();
        afterThird.RequiresInvoice.Should().BeFalse();
    }

    [Fact]
    public async Task Tipo_y_categoria_siguen_actualizandose_sin_reglas_fiscales()
    {
        var s = await SeedAsync(deductible: false, requiresInvoice: false);
        await UpdateAsync(s, new(s.TypeId, "T1X", "Tipo nuevo", null));
        await UpdateAsync(s, new(s.CategoryId, "C1X", "Cat nueva", null, "d"));

        var type = await LoadAsync(s, s.TypeId);
        type.Name.Should().Be("Tipo nuevo");
        type.IsDeductible.Should().BeFalse();
        type.RequiresInvoice.Should().BeFalse();
        var cat = await LoadAsync(s, s.CategoryId);
        cat.Code.Should().Be("C1X");
        cat.IsDeductible.Should().BeFalse();
        cat.RequiresInvoice.Should().BeFalse();
        var sub = await LoadAsync(s, s.SubId);
        sub.IsDeductible.Should().BeFalse();
        sub.RequiresInvoice.Should().BeFalse();
    }
}
