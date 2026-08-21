using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Communications.UseCases.SendTestEmail;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Communications;

/// <summary>COMMUNICATIONS-SETTINGS-UI-01 — SendTestEmail usa la configuración SMTP resuelta de la empresa actual.</summary>
public sealed class SendTestEmailHandlerTests
{
    private sealed class Fixture
    {
        public Mock<ICommunicationSettingsResolver> SettingsResolver { get; } = new();
        public Mock<IEmailSender> EmailSender { get; } = new();

        public SendTestEmailCommandHandler BuildHandler() =>
            new(SettingsResolver.Object, EmailSender.Object);
    }

    private static readonly CommunicationEmailSettings ReadySettings = new(
        Enabled: true,
        SmtpHost: "smtp.zoho.com",
        SmtpPort: 587,
        SmtpUsername: "facturacion@empresa.com",
        SmtpPassword: "secret",
        SenderEmail: "facturacion@empresa.com",
        SenderName: "Empresa Piloto",
        UseSsl: true,
        ReplyToEmail: null,
        MaxRetries: 3,
        DefaultLanguage: "es"
    );

    [Fact]
    public async Task SendTestEmail_usa_la_configuracion_resuelta_de_la_empresa_actual()
    {
        var f = new Fixture();
        f.SettingsResolver.Setup(s => s.ResolveEmailAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ReadySettings);

        var result = await f.BuildHandler().Handle(new SendTestEmailCommand("destino@cliente.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.EmailSender.Verify(
            e => e.SendAsync(
                It.Is<EmailMessage>(m => m.ToEmail == "destino@cliente.com"),
                ReadySettings,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTestEmail_con_configuracion_incompleta_no_intenta_enviar()
    {
        var f = new Fixture();
        f.SettingsResolver
            .Setup(s => s.ResolveEmailAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReadySettings with { Enabled = false });

        var result = await f.BuildHandler().Handle(new SendTestEmailCommand("destino@cliente.com"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.EmailSender.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CommunicationEmailSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
