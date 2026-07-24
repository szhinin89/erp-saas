using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.UseCases.RetryElectronicDocument;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// Company Scope (IDOR): un documento electrónico pertenece a una única empresa (CompanyId).
/// El reintento manual (Monitor) no debe poder actuar sobre un documento de una empresa distinta
/// a la empresa activa del usuario, aunque ambas empresas pertenezcan al mismo tenant.
/// </summary>
public sealed class RetryElectronicDocumentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwningCompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string ValidAccessKey = new('1', 49);

    private static ElectronicDocument NewReceivedDocument(Guid companyId)
    {
        var document = ElectronicDocument.Create(TenantId, companyId, ElectronicDocumentType.Invoice, "Sales", Guid.NewGuid(), UserId);
        document.MarkXmlGenerated("draft/path.xml", "1.1.0", "1.1.0", UserId);
        document.MarkSigned("signed/path.xml", AccessKey.Create(ValidAccessKey), UserId);
        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        return document;
    }

    private static Mock<ICurrentCompany> CompanyContext(Guid companyId)
    {
        var mock = new Mock<ICurrentCompany>();
        mock.SetupGet(c => c.CompanyId).Returns(companyId);
        mock.SetupGet(c => c.HasCompanyContext).Returns(true);
        mock.SetupGet(c => c.IsAuthenticated).Returns(true);
        return mock;
    }

    private static Mock<ICurrentTenant> TenantContext()
    {
        var mock = new Mock<ICurrentTenant>();
        mock.SetupGet(t => t.TenantId).Returns(TenantId);
        return mock;
    }

    private static Mock<ICurrentUser> UserContext()
    {
        var mock = new Mock<ICurrentUser>();
        mock.SetupGet(u => u.UserId).Returns(UserId);
        return mock;
    }

    private static Mock<ISourceDocumentSummaryProviderResolver> NoSummaryResolver()
    {
        var mock = new Mock<ISourceDocumentSummaryProviderResolver>();
        mock.Setup(r => r.Resolve(It.IsAny<string>())).Returns((ISourceDocumentSummaryProvider?)null);
        return mock;
    }

    private static Mock<ICompanyRepository> NoCompanyRepository()
    {
        var mock = new Mock<ICompanyRepository>();
        mock.Setup(r => r.GetByIdForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Modules.Company.Entities.Company?)null);
        return mock;
    }

    private static Mock<IAuditReader<ElectronicDocumentAudit>> EmptyAuditReader()
    {
        var mock = new Mock<IAuditReader<ElectronicDocumentAudit>>();
        mock.Setup(r => r.GetByEntityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ElectronicDocumentAudit>());
        return mock;
    }

    private static Mock<IAuditReader<ElectronicDocumentSriMessage>> EmptySriMessageReader()
    {
        var mock = new Mock<IAuditReader<ElectronicDocumentSriMessage>>();
        mock.Setup(r => r.GetByEntityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ElectronicDocumentSriMessage>());
        return mock;
    }

    private static RetryElectronicDocumentCommandHandler NewHandler(
        Mock<IElectronicDocumentIssuer> issuer, Mock<IElectronicDocumentRepository> repository,
        Mock<ICurrentTenant> tenant, Mock<ICurrentCompany> company, Mock<ICurrentUser> user)
        => new(
            issuer.Object, repository.Object, NoSummaryResolver().Object, NoCompanyRepository().Object,
            EmptyAuditReader().Object, EmptySriMessageReader().Object, tenant.Object, company.Object, user.Object);

    [Fact]
    public async Task Handle_when_document_belongs_to_another_company_in_same_tenant_returns_not_found()
    {
        var otherCompanyId = Guid.NewGuid();
        var document = NewReceivedDocument(OwningCompanyId);

        var repository = new Mock<IElectronicDocumentRepository>();
        repository.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var issuer = new Mock<IElectronicDocumentIssuer>();

        var handler = NewHandler(issuer, repository, TenantContext(), CompanyContext(otherCompanyId), UserContext());

        var result = await handler.Handle(new RetryElectronicDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        issuer.Verify(i => i.RetryAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_when_document_belongs_to_active_company_delegates_to_issuer()
    {
        var document = NewReceivedDocument(OwningCompanyId);

        var repository = new Mock<IElectronicDocumentRepository>();
        repository.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var issuer = new Mock<IElectronicDocumentIssuer>();
        issuer.Setup(i => i.RetryAsync(TenantId, document.Id, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentDto>.Success(null!));

        var handler = NewHandler(issuer, repository, TenantContext(), CompanyContext(OwningCompanyId), UserContext());

        var result = await handler.Handle(new RetryElectronicDocumentCommand(document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(document.Id);
        issuer.Verify(i => i.RetryAsync(TenantId, document.Id, UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_when_document_does_not_exist_returns_not_found()
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        repository.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ElectronicDocument?)null);

        var issuer = new Mock<IElectronicDocumentIssuer>();

        var handler = NewHandler(issuer, repository, TenantContext(), CompanyContext(OwningCompanyId), UserContext());

        var result = await handler.Handle(new RetryElectronicDocumentCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        issuer.Verify(i => i.RetryAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
