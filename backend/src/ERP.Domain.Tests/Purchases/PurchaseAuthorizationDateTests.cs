using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;
using System.Text.Json;

namespace ERP.Domain.Tests.Purchases;

public sealed class PurchaseAuthorizationDateTests
{
    /// <summary>
    /// ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02: solo instantes con zona conocida. "...Z" y offset
    /// explícito se conservan como el mismo instante UTC; una hora sin zona ya no se etiqueta UTC
    /// (ver <see cref="Hora_sin_zona_se_rechaza_en_vez_de_etiquetarse_UTC"/>).
    /// </summary>
    public static IEnumerable<object?[]> Dates()
    {
        yield return new object?[] { JsonSerializer.Deserialize<DateTime>("\"2026-09-03T16:50:00-05:00\""), new DateTime(2026, 9, 3, 21, 50, 0, DateTimeKind.Utc) };
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

    [Fact]
    public void Hora_sin_zona_se_rechaza_en_vez_de_etiquetarse_UTC()
    {
        // Antes: "2026-09-03T21:50" (hora Ecuador) se guardaba como 21:50Z — +5h de drift.
        var wallClock = JsonSerializer.Deserialize<DateTime>("\"2026-09-03T21:50\"");
        wallClock.Kind.Should().Be(DateTimeKind.Unspecified);
        var id = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 3);

        var purchase = () => PurchaseInvoice.CreateDraft(id, id, id, id, "Supplier", "1791352688001",
            "01", "001-001-000000001", date, id, id, "Contado", 1, 0, authorizationDate: wallClock);
        var expense = () => ExpenseDocument.CreateDraft(id, id, id, id, "Supplier", "1791352688001",
            date, date, "01", "001-001-000000001", id, "Contado", 1, 0, id, authorizationDate: wallClock);

        purchase.Should().Throw<ArgumentException>();
        expense.Should().Throw<ArgumentException>();
    }

    private static void AssertDate(DateTime? actual, DateTime? expected)
    {
        actual.Should().Be(expected);
        actual?.Kind.Should().Be(DateTimeKind.Utc);
    }
}
