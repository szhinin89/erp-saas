using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Communications;

public sealed class CommunicationOutboxTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void CreateEmail_normaliza_destinatario_y_queda_pendiente()
    {
        var scheduledAt = DateTime.UtcNow.AddMinutes(5);

        var message = CommunicationOutbox.CreateEmail(
            TenantId,
            CompanyId,
            BranchId,
            "SalesInvoiceAuthorized",
            " Cliente ",
            " CLIENTE@MAIL.COM ",
            " Factura autorizada ",
            "<p>Lista</p>",
            null,
            CommunicationPriority.High,
            scheduledAt,
            5,
            "SalesInvoice",
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "invoice:001",
            UserId
        );

        message.TenantId.Should().Be(TenantId);
        message.CompanyId.Should().Be(CompanyId);
        message.BranchId.Should().Be(BranchId);
        message.Channel.Should().Be(CommunicationChannel.Email);
        message.Status.Should().Be(CommunicationStatus.Pending);
        message.Priority.Should().Be(CommunicationPriority.High);
        message.RecipientEmail.Should().Be("cliente@mail.com");
        message.Subject.Should().Be("Factura autorizada");
        message.MaxRetries.Should().Be(5);
        message.IdempotencyKey.Should().Be("invoice:001");
        message.IsDue(scheduledAt.AddSeconds(-1)).Should().BeFalse();
        message.IsDue(scheduledAt).Should().BeTrue();
    }

    [Fact]
    public void AddAttachment_exige_ruta_o_contenido_binario()
    {
        var message = NewMessage(maxRetries: 3);

        message.AddAttachment(
            CommunicationAttachmentType.AuthorizedXml,
            "autorizado.xml",
            "application/xml",
            "storage/invoices/autorizado.xml",
            null,
            UserId
        );

        message.Attachments.Should().ContainSingle();
        message.Attachments.Single().TenantId.Should().Be(TenantId);
        message.Attachments.Single().CompanyId.Should().Be(CompanyId);
        message.Attachments.Single().CommunicationOutboxId.Should().Be(message.Id);
    }

    [Fact]
    public void MarkFailed_reprograma_hasta_agotar_reintentos()
    {
        var message = NewMessage(maxRetries: 2);

        message.MarkProcessing(UserId);
        message.MarkFailed("SMTP temporal", UserId);

        message.Status.Should().Be(CommunicationStatus.Pending);
        message.RetryCount.Should().Be(1);
        message.NextAttemptAtUtc.Should().NotBeNull();
        message.LastError.Should().Be("SMTP temporal");

        message.MarkProcessing(UserId);
        message.MarkFailed("SMTP final", UserId);

        message.Status.Should().Be(CommunicationStatus.Failed);
        message.RetryCount.Should().Be(2);
        message.NextAttemptAtUtc.Should().BeNull();
        message.LastError.Should().Be("SMTP final");
    }

    [Fact]
    public void MarkSent_limpia_error_y_cierra_envio()
    {
        var message = NewMessage(maxRetries: 2);

        message.MarkProcessing(UserId);
        message.MarkFailed("SMTP temporal", UserId);
        message.MarkProcessing(UserId);
        message.MarkSent(UserId);

        message.Status.Should().Be(CommunicationStatus.Sent);
        message.SentAtUtc.Should().NotBeNull();
        message.LastError.Should().BeNull();
        message.NextAttemptAtUtc.Should().BeNull();
    }

    private static CommunicationOutbox NewMessage(int maxRetries) =>
        CommunicationOutbox.CreateEmail(
            TenantId,
            CompanyId,
            null,
            "SalesInvoiceAuthorized",
            "Cliente",
            "cliente@mail.com",
            "Factura autorizada",
            "<p>Lista</p>",
            null,
            CommunicationPriority.Normal,
            DateTime.UtcNow.AddMinutes(-1),
            maxRetries,
            "SalesInvoice",
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            UserId
        );
}
