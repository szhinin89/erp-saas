using ERP.Application.Common.Models;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;
using FluentAssertions;
using Xunit;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

public sealed class ImportPurchaseReceptionValidatorTests
{
    private readonly ImportPurchaseReceptionValidator _validator = new();

    [Fact]
    public void Fails_when_file_is_empty()
    {
        var content = new MediaUploadContent(new MemoryStream(), "reception.txt", "text/plain", 0);
        var result = _validator.Validate(new ImportPurchaseReceptionCommand(content));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("vacío"));
    }

    [Fact]
    public void Fails_when_file_extension_is_not_txt()
    {
        var content = new MediaUploadContent(new MemoryStream([1]), "reception.xml", "text/xml", 1);
        var result = _validator.Validate(new ImportPurchaseReceptionCommand(content));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(".txt"));
    }

    [Fact]
    public void Succeeds_for_a_valid_txt_file()
    {
        var content = new MediaUploadContent(new MemoryStream([1]), "reception.txt", "text/plain", 1);
        var result = _validator.Validate(new ImportPurchaseReceptionCommand(content));

        result.IsValid.Should().BeTrue();
    }
}
