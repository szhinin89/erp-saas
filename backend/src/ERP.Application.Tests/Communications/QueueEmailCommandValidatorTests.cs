using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.UseCases.QueueEmail;
using ERP.Domain.Modules.Communications.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Communications;

public sealed class QueueEmailCommandValidatorTests
{
    private readonly QueueEmailCommandValidator _validator = new();

    [Fact]
    public void Comando_valido_con_adjunto_por_ruta_pasa_validacion()
    {
        var command = new QueueEmailCommand(
            Purpose: "SalesInvoiceAuthorized",
            RecipientName: "Cliente",
            RecipientEmail: "cliente@mail.com",
            Subject: "Factura autorizada",
            BodyHtml: "<p>Lista</p>",
            BodyText: null,
            Priority: CommunicationPriority.Normal,
            ScheduledAtUtc: DateTime.UtcNow,
            MaxRetries: 3,
            CorrelationType: "SalesInvoice",
            CorrelationId: Guid.NewGuid(),
            IdempotencyKey: "sales-invoice:001:cliente@mail.com",
            Attachments:
            [
                new QueueCommunicationAttachmentDto(
                    CommunicationAttachmentType.RidePdf,
                    "ride.pdf",
                    "application/pdf",
                    "storage/ride.pdf"
                ),
            ]
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Comando_sin_cuerpo_y_con_email_invalido_falla_validacion()
    {
        var command = new QueueEmailCommand(
            Purpose: "SalesInvoiceAuthorized",
            RecipientName: "Cliente",
            RecipientEmail: "correo-invalido",
            Subject: "Factura autorizada",
            BodyHtml: null,
            BodyText: null
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain(nameof(QueueEmailCommand.RecipientEmail));
        result.Errors.Select(e => e.ErrorMessage).Should().Contain("La comunicación debe tener cuerpo HTML o texto.");
    }
}
