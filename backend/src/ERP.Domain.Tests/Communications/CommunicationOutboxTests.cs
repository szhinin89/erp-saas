using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Communications;

public sealed class CommunicationOutboxTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid InvoiceId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static CommunicationIdentity InvoiceIdentity(Guid? branchId = null) =>
        CommunicationIdentity.For(
            CommunicationScope.Company(TenantId, CompanyId, branchId),
            CommunicationPurposes.SalesInvoiceAuthorized,
            CommunicationChannel.Email,
            new CommunicationSource("Sales", "SalesInvoice", InvoiceId),
            CommunicationRecipientRole.Customer
        );

    [Fact]
    public void CreateEmail_toma_alcance_origen_rol_e_identidad_de_la_identidad()
    {
        var scheduledAt = DateTime.UtcNow.AddMinutes(5);
        var identity = InvoiceIdentity(BranchId);

        var message = CommunicationOutbox.CreateEmail(
            identity, " Cliente ", " CLIENTE@MAIL.COM ", " Factura autorizada ", "<p>Lista</p>", null,
            CommunicationPriority.High, scheduledAt, 5, UserId
        );

        message.ScopeKind.Should().Be(CommunicationScopeKind.Company);
        message.TenantId.Should().Be(TenantId);
        message.CompanyId.Should().Be(CompanyId);
        message.BranchId.Should().Be(BranchId);
        message.Channel.Should().Be(CommunicationChannel.Email);
        message.Purpose.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        message.SourceModule.Should().Be("Sales");
        message.SourceType.Should().Be("SalesInvoice");
        message.SourceId.Should().Be(InvoiceId);
        message.RecipientRole.Should().Be(CommunicationRecipientRole.Customer);
        message.Status.Should().Be(CommunicationStatus.Pending);
        message.RecipientEmail.Should().Be("cliente@mail.com");
        message.Subject.Should().Be("Factura autorizada");
        message.MaxRetries.Should().Be(5);
        message.IdempotencyKey.Should().Be(identity.Key);
        message.ResendSequence.Should().Be(0);
        message.ResendOfCommunicationId.Should().BeNull();
        message.Scope.Should().Be(identity.Scope);
    }

    [Fact]
    public void Comunicacion_System_no_tiene_tenant_empresa_ni_sucursal()
    {
        var identity = CommunicationIdentity.For(
            CommunicationScope.System,
            CommunicationPurposes.PasswordReset,
            CommunicationChannel.Email,
            new CommunicationSource("Authentication", "PasswordReset", Guid.NewGuid()),
            CommunicationRecipientRole.User
        );

        var message = CommunicationOutbox.CreateEmail(identity, null, "u@test.com", "Recupera tu acceso", null, "texto", CommunicationPriority.High, null, 3, Guid.Empty);
        message.AddAttachment(CommunicationAttachmentType.Generic, "a.txt", "text/plain", null, [1], Guid.Empty);

        message.ScopeKind.Should().Be(CommunicationScopeKind.System);
        message.TenantId.Should().BeNull();
        message.CompanyId.Should().BeNull();
        message.BranchId.Should().BeNull();
        message.Attachments.Single().TenantId.Should().BeNull();
        message.Attachments.Single().CompanyId.Should().BeNull();
    }

    [Fact]
    public void Reenvio_manual_exige_identidad_de_reenvio_y_original_y_viceversa()
    {
        var original = InvoiceIdentity();
        var originalId = Guid.NewGuid();

        var resend = CommunicationOutbox.CreateEmail(
            original.ForResend(1), null, "c@test.com", "s", "<p>x</p>", null, CommunicationPriority.Normal, null, 3, UserId, originalId);
        resend.ResendOfCommunicationId.Should().Be(originalId);
        resend.ResendSequence.Should().Be(1);
        resend.IdempotencyKey.Should().NotBe(original.Key);

        var sinOriginal = () => CommunicationOutbox.CreateEmail(original.ForResend(1), null, "c@test.com", "s", "<p>x</p>", null, CommunicationPriority.Normal, null, 3, UserId);
        var originalConReferencia = () => CommunicationOutbox.CreateEmail(original, null, "c@test.com", "s", "<p>x</p>", null, CommunicationPriority.Normal, null, 3, UserId, originalId);
        sinOriginal.Should().Throw<ArgumentException>();
        originalConReferencia.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddAttachment_hereda_el_alcance_y_exige_ruta_o_contenido_binario()
    {
        var message = CommunicationOutbox.CreateEmail(InvoiceIdentity(), "Cliente", "cliente@mail.com", "Factura", "<p>Lista</p>", null, CommunicationPriority.Normal, null, 3, UserId);

        message.AddAttachment(CommunicationAttachmentType.AuthorizedXml, "autorizado.xml", "application/xml", "storage/invoices/autorizado.xml", null, UserId);
        var sinContenido = () => message.AddAttachment(CommunicationAttachmentType.Generic, "x", "text/plain", null, null, UserId);

        message.Attachments.Should().ContainSingle();
        message.Attachments.Single().TenantId.Should().Be(TenantId);
        message.Attachments.Single().CompanyId.Should().Be(CompanyId);
        message.Attachments.Single().CommunicationOutboxId.Should().Be(message.Id);
        sinContenido.Should().Throw<ArgumentException>();
    }
}
