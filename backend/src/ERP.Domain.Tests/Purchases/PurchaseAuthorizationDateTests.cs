using System.Text.Json;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

public sealed class PurchaseAuthorizationDateTests
{
    public static IEnumerable<object?[]> Dates()
    {
        yield return new object?[] { JsonSerializer.Deserialize<DateTime>("\"2026-09-03T21:50\""), new DateTime(2026, 9, 3, 21, 50, 0, DateTimeKind.Utc) };
        yield return new object?[] { new DateTime(2026, 9, 3, 21, 50, 0, DateTimeKind.Utc), new DateTime(2026, 9, 3, 21, 50, 0, DateTimeKind.Utc) };
        var local = new DateTime(2026, 9, 3, 21, 50, 0, DateTimeKind.Local);
        yield return new object?[] { local, local.ToUniversalTime() };
        yield return new object?[] { null, null };
    }

    [Theory]
    [MemberData(nameof(Dates))]
    public void Purchase_create_and_update_normalize_authorization_before_persistence(DateTime? input, DateTime? expected)
    {
        var id = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 3);
        var invoice = PurchaseInvoice.CreateDraft(id, id, id, id, "Supplier", "1791352688001",
            "01", "001-001-000000001", date, id, id, "Contado", 1, 0,
            authorizationDate: input);

        AssertDate(invoice.AuthorizationDate, expected);
        invoice.UpdateDraft(id, "Supplier", "1791352688001", "01", "001-001-000000001",
            date, id, authorizationDate: input);
        AssertDate(invoice.AuthorizationDate, expected);
        invoice.IssueDate.Should().Be(date);
    }

    [Theory]
    [MemberData(nameof(Dates))]
    public void Expense_from_reception_create_and_update_normalize_authorization(DateTime? input, DateTime? expected)
    {
        var id = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 3);
        var document = ExpenseDocument.CreateDraft(id, id, id, id, "Supplier", "1791352688001",
            date, date, "01", "001-001-000000001", id, "Contado", 1, 0, id,
            authorizationDate: input, receptionDocumentId: id);

        AssertDate(document.AuthorizationDate, expected);
        document.UpdateDraft(id, "Supplier", "1791352688001", date, date, "01",
            "001-001-000000001", id, "Contado", 1, 0, id, authorizationDate: input);
        AssertDate(document.AuthorizationDate, expected);
        document.ReceptionDocumentId.Should().Be(id);
        document.IssueDate.Should().Be(date);
        document.AccountingDate.Should().Be(date);
    }

    private static void AssertDate(DateTime? actual, DateTime? expected)
    {
        actual.Should().Be(expected);
        actual?.Kind.Should().Be(DateTimeKind.Utc);
    }
}
