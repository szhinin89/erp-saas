using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases;

public sealed class PurchaseCreditNoteFiscalIdentityTests
{
    private const string Key = "2110202604179135268800110010010000000991234567810";

    [Theory]
    [InlineData("001-001-000000099", true)]
    [InlineData("1-1-99", false)]
    [InlineData("001001000000099", false)]
    [InlineData("001-001-000000098", false)]
    [InlineData(null, false)]
    public void Fiscal_identity_does_not_allow_number_aliases_or_a_key_for_another_sequence(string? number, bool valid)
    {
        var command = new CreateDraftPurchaseCreditNoteCommand(Guid.NewGuid(), Guid.NewGuid(), null,
            PurchaseCreditNoteApplicationType.Discount, number!, Key, null, null, new DateOnly(2026, 10, 21),
            "Bonificación", [new("Ajuste", 1m, null, null, 0m)]);
        new CreateDraftPurchaseCreditNoteValidator().Validate(command).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Key_must_represent_a_credit_note_from_the_selected_supplier()
    {
        PurchaseCreditNoteFiscalIdentity.IsCompatible("001-001-000000099", Key[..8] + "01" + Key[10..]).Should().BeFalse();
        PurchaseCreditNoteFiscalIdentity.HasDifferentIssuer(Key, "1791352688001").Should().BeFalse();
        PurchaseCreditNoteFiscalIdentity.HasDifferentIssuer(Key, "1799999999001").Should().BeTrue();
    }
}
