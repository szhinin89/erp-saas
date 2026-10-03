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

    private static CommunicationTemplateUsage Usage(CommunicationIdentity identity) =>
        new(identity.Purpose, 1, CommunicationTemplateSource.Default);

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
            identity, " Cliente ", " CLIENTE@MAIL.COM ", Usage(identity), " Factura autorizada ", "<p>Lista</p>", null,
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
        message.TemplateKey.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        message.TemplateVersion.Should().Be(1);
        message.TemplateSource.Should().Be(CommunicationTemplateSource.Default);
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

        var message = CommunicationOutbox.CreateEmail(identity, null, "u@test.com", Usage(identity), "Recupera tu acceso", null, "texto", CommunicationPriority.High, null, 3, Guid.Empty);
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
            original.ForResend(1), null, "c@test.com", Usage(original), "s", "<p>x</p>", null, CommunicationPriority.Normal, null, 3, UserId, originalId);
        resend.ResendOfCommunicationId.Should().Be(originalId);
        resend.ResendSequence.Should().Be(1);
        resend.IdempotencyKey.Should().NotBe(original.Key);

        var sinOriginal = () => CommunicationOutbox.CreateEmail(original.ForResend(1), null, "c@test.com", Usage(original), "s", "<p>x</p>", null, CommunicationPriority.Normal, null, 3, UserId);
        var originalConReferencia = () => CommunicationOutbox.CreateEmail(original, null, "c@test.com", Usage(original), "s", "<p>x</p>", null, CommunicationPriority.Normal, null, 3, UserId, originalId);
        sinOriginal.Should().Throw<ArgumentException>();
        originalConReferencia.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddAttachment_hereda_el_alcance_y_exige_ruta_o_contenido_binario()
    {
        var message = CommunicationOutbox.CreateEmail(InvoiceIdentity(), "Cliente", "cliente@mail.com", Usage(InvoiceIdentity()), "Factura", "<p>Lista</p>", null, CommunicationPriority.Normal, null, 3, UserId);

        message.AddAttachment(CommunicationAttachmentType.AuthorizedXml, "autorizado.xml", "application/xml", "storage/invoices/autorizado.xml", null, UserId);
        var sinContenido = () => message.AddAttachment(CommunicationAttachmentType.Generic, "x", "text/plain", null, null, UserId);

        message.Attachments.Should().ContainSingle();
        message.Attachments.Single().TenantId.Should().Be(TenantId);
        message.Attachments.Single().CompanyId.Should().Be(CompanyId);
        message.Attachments.Single().CommunicationOutboxId.Should().Be(message.Id);
        sinContenido.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Template_de_otro_proposito_o_uso_invalido_falla()
    {
        var identity = InvoiceIdentity();
        var otherKey = () => CommunicationOutbox.CreateEmail(
            identity, null, "c@test.com", new CommunicationTemplateUsage(CommunicationPurposes.PasswordReset, 1, CommunicationTemplateSource.Default),
            "s", "<p>x</p>", null, CommunicationPriority.Normal, null, 3, UserId);
        var versionZero = () => new CommunicationTemplateUsage(CommunicationPurposes.SalesInvoiceAuthorized, 0, CommunicationTemplateSource.Default);
        var legacyNew = () => new CommunicationTemplateUsage(CommunicationPurposes.SalesInvoiceAuthorized, 1, CommunicationTemplateSource.Legacy);

        otherKey.Should().Throw<ArgumentException>();
        versionZero.Should().Throw<ArgumentOutOfRangeException>();
        legacyNew.Should().Throw<ArgumentException>("Legacy solo existe en filas previas al subsistema");
    }

    [Fact]
    public void Override_de_empresa_sube_de_revision_en_cada_cambio_de_contenido()
    {
        var template = CommunicationTemplate.Create(TenantId, CompanyId, null, CommunicationPurposes.SalesInvoiceAuthorized, "Factura", CommunicationChannel.Email, "Asunto {{InvoiceNumber}}", "<p>x</p>", null, "es", UserId);
        template.Revision.Should().Be(1);

        template.UpdateContent("Factura", "Otro {{InvoiceNumber}}", "<p>y</p>", null, UserId);

        template.Revision.Should().Be(2);
    }

    [Fact]
    public void Fallo_de_template_nace_Failed_sin_contenido_y_respeta_sus_invariantes()
    {
        var identity = InvoiceIdentity();

        var failed = CommunicationOutbox.CreateEmailTemplateFailure(
            identity, "Cliente", "Cliente@Mail.com", CommunicationFailureCategory.Configuration,
            "COMMUNICATION_TEMPLATE_INVALID: placeholder no declarado", "{\"InvoiceNumber\":\"001\"}",
            CommunicationPriority.Normal, 3, UserId);

        failed.Status.Should().Be(CommunicationStatus.Failed);
        failed.FailureCategory.Should().Be(CommunicationFailureCategory.Configuration);
        failed.Subject.Should().BeNull();
        failed.BodyHtml.Should().BeNull();
        failed.BodyText.Should().BeNull();
        failed.IdempotencyKey.Should().Be(identity.Key, "misma identidad: repetir el hecho no la duplica");
        failed.TemplateKey.Should().Be(identity.Purpose);
        failed.TemplatePayloadJson.Should().Contain("InvoiceNumber");
        failed.RecipientEmail.Should().Be("cliente@mail.com");

        var transient = () => CommunicationOutbox.CreateEmailTemplateFailure(identity, null, "c@test.com", CommunicationFailureCategory.Transient, "x", null, CommunicationPriority.Normal, 3, UserId);
        var resend = () => CommunicationOutbox.CreateEmailTemplateFailure(identity.ForResend(1), null, "c@test.com", CommunicationFailureCategory.Configuration, "x", null, CommunicationPriority.Normal, 3, UserId);
        var sensitivePayload = () => CommunicationOutbox.CreateEmailTemplateFailure(
            CommunicationIdentity.For(CommunicationScope.System, CommunicationPurposes.PasswordReset, CommunicationChannel.Email,
                new CommunicationSource("Authentication", "PasswordReset", Guid.NewGuid()), CommunicationRecipientRole.User),
            null, "u@test.com", CommunicationFailureCategory.Configuration, "x", "{\"Link\":\"secreto\"}", CommunicationPriority.High, 3, UserId);

        transient.Should().Throw<ArgumentException>();
        resend.Should().Throw<ArgumentException>();
        sensitivePayload.Should().Throw<ArgumentException>();
    }
}
