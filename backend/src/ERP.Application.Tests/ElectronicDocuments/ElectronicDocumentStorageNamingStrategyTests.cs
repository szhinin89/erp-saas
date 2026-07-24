using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentStorageNamingStrategyTests
{
    [Fact]
    public void BuildRelativePath_draft_and_signed_produce_distinct_deterministic_paths()
    {
        var strategy = new ElectronicDocumentStorageNamingStrategy();
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        var draft = strategy.BuildRelativePath(tenantId, ElectronicDocumentType.Invoice, docId, ElectronicDocumentXmlVariant.Draft);
        var signed = strategy.BuildRelativePath(tenantId, ElectronicDocumentType.Invoice, docId, ElectronicDocumentXmlVariant.Signed);

        draft.Should().NotBe(signed);
        draft.Should().EndWith("draft.xml");
        signed.Should().EndWith("signed.xml");
        draft.Should().Contain(tenantId.ToString("N"));
        draft.Should().Contain(docId.ToString("N"));
        draft.Should().Contain("invoice");
    }

    [Fact]
    public void BuildRelativePath_is_deterministic_for_same_inputs()
    {
        var strategy = new ElectronicDocumentStorageNamingStrategy();
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        var first = strategy.BuildRelativePath(tenantId, ElectronicDocumentType.Invoice, docId, ElectronicDocumentXmlVariant.Draft);
        var second = strategy.BuildRelativePath(tenantId, ElectronicDocumentType.Invoice, docId, ElectronicDocumentXmlVariant.Draft);

        first.Should().Be(second);
    }
}
