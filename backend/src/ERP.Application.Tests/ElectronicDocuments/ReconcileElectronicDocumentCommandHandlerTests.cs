using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.UseCases.GetElectronicDocumentDetail;
using ERP.Application.Modules.ElectronicDocuments.UseCases.ReconcileElectronicDocument;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// Caso #32 — conciliación manual: evidencia obligatoria, scope de empresa, solo estados con
/// resultado SRI desconocido; reconsulta por el mismo issuer (que nunca reenvía) y nunca autoriza
/// por declaración del usuario.
/// </summary>
public sealed class ReconcileElectronicDocumentCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly string Key = new('1', 49);

    private static ElectronicDocument Received()
    {
        var document = ElectronicDocument.Create(
            TenantId,
            CompanyId,
            ElectronicDocumentType.Invoice,
            "Sales",
            Guid.NewGuid(),
            UserId
        );
        document.MarkXmlGenerated("draft.xml", "1.1.0", "1.1.0", UserId);
        document.MarkSigned("signed.xml", AccessKey.Create(Key), UserId);
        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        return document;
    }

    private sealed record Sut(
        ReconcileElectronicDocumentCommandHandler Handler,
        Mock<IElectronicDocumentIssuer> Issuer,
        Mock<IElectronicDocumentRepository> Repository,
        Mock<IMediator> Mediator
    );

    private static Sut Build(ElectronicDocument document, Guid activeCompanyId)
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        var issuer = new Mock<IElectronicDocumentIssuer>();
        issuer
            .Setup(i => i.RetryAsync(TenantId, document.Id, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentDto>.Failure("SRI sin respuesta"));
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m =>
                m.Send(It.IsAny<GetElectronicDocumentDetailQuery>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<ElectronicDocumentDetailDto>.Success(null!));
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.SetupGet(c => c.CompanyId).Returns(activeCompanyId);
        company.SetupGet(c => c.HasCompanyContext).Returns(true);
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.UserId).Returns(UserId);

        var handler = new ReconcileElectronicDocumentCommandHandler(
            issuer.Object,
            repository.Object,
            mediator.Object,
            tenant.Object,
            company.Object,
            user.Object
        );
        return new Sut(handler, issuer, repository, mediator);
    }

    [Fact]
    public async Task Records_evidence_then_requeries_and_returns_detail_even_if_query_is_inconclusive()
    {
        var document = Received();
        document.ClearDomainEvents();
        var sut = Build(document, CompanyId);

        var result = await sut.Handler.Handle(
            new ReconcileElectronicDocumentCommand(document.Id, "SRI en Línea", "Sin registro"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        document.CurrentState.Should().Be(ElectronicDocumentState.Received);
        document
            .DomainEvents.OfType<ElectronicDocumentReconciliationRecordedEvent>()
            .Should()
            .ContainSingle();
        sut.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        sut.Issuer.Verify(
            i => i.RetryAsync(TenantId, document.Id, UserId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Document_of_another_company_returns_not_found_without_evidence()
    {
        var document = Received();
        document.ClearDomainEvents();
        var sut = Build(document, Guid.NewGuid());

        var result = await sut.Handler.Handle(
            new ReconcileElectronicDocumentCommand(document.Id, "ref", "obs"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        document.DomainEvents.Should().BeEmpty();
        sut.Issuer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Document_with_known_outcome_is_rejected_without_evidence_or_query()
    {
        var document = Received();
        document.MarkAuthorized(AuthorizationNumber.Create(Key), DateTime.UtcNow, null, UserId);
        document.ClearDomainEvents();
        var sut = Build(document, CompanyId);

        var result = await sut.Handler.Handle(
            new ReconcileElectronicDocumentCommand(document.Id, "ref", "obs"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        document.DomainEvents.Should().BeEmpty();
        sut.Issuer.VerifyNoOtherCalls();
    }

    [Fact]
    public void Validator_requires_reference_and_observation_within_limits()
    {
        var validator = new ReconcileElectronicDocumentCommandValidator();

        validator
            .Validate(new ReconcileElectronicDocumentCommand(Guid.NewGuid(), "", " "))
            .Errors.Should()
            .HaveCount(2);
        validator
            .Validate(
                new ReconcileElectronicDocumentCommand(
                    Guid.NewGuid(),
                    new string('r', ElectronicDocument.ReconciliationReferenceMaxLen + 1),
                    "obs"
                )
            )
            .IsValid.Should()
            .BeFalse();
    }
}
