using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.BatchDownloadPurchaseReceptionXml;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

public sealed class BatchDownloadPurchaseReceptionXmlValidatorTests
{
    private readonly BatchDownloadPurchaseReceptionXmlValidator _validator = new();

    [Fact]
    public void Rejects_an_empty_batch()
    {
        var result = _validator.Validate(new BatchDownloadPurchaseReceptionXmlCommand([]));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_batch_larger_than_the_maximum()
    {
        var ids = Enumerable.Range(0, BatchDownloadPurchaseReceptionXmlValidator.MaxBatchSize + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();
        var result = _validator.Validate(new BatchDownloadPurchaseReceptionXmlCommand(ids));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_onlyMissingXml_false()
    {
        var result = _validator.Validate(
            new BatchDownloadPurchaseReceptionXmlCommand([Guid.NewGuid()], OnlyMissingXml: false)
        );
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Accepts_a_valid_batch()
    {
        var ids = Enumerable.Range(0, BatchDownloadPurchaseReceptionXmlValidator.MaxBatchSize)
            .Select(_ => Guid.NewGuid())
            .ToList();
        var result = _validator.Validate(new BatchDownloadPurchaseReceptionXmlCommand(ids));
        result.IsValid.Should().BeTrue();
    }
}
