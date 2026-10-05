using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Infrastructure.Communications;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — transporte SMTP: Message-ID determinístico, timeout
/// explícito y clasificación estructurada de fallos.
/// </summary>
public sealed class SmtpDeliveryTransportTests
{
    private static CommunicationEmailSettings Settings(TimeSpan? timeout = null, int port = 587) =>
        new(
            true,
            "127.0.0.1",
            port,
            null,
            null,
            "facturacion@empresa.test",
            "Empresa",
            false,
            null,
            3,
            "es"
        )
        {
            SmtpTimeout = timeout ?? CommunicationDeliveryTiming.DefaultSmtpTimeout,
        };

    private static EmailMessage Message(Guid? communicationId) =>
        new("cliente@test.com", "Cliente", "Factura", "<p>x</p>", null, [], communicationId);

    // ── Message-ID ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Message_ID_es_estable_por_comunicacion_y_no_expone_destinatario()
    {
        var id = Guid.NewGuid();

        var first = WriteEml(Message(id));
        var retry = WriteEml(Message(id));

        var expected = $"Message-ID: <{id:N}@{CommunicationMessageId.Domain}>";
        first.Should().Contain(expected);
        retry.Should().Contain(expected, "mismo Message-ID en todos los reintentos");
        CountOccurrences(first, "Message-ID:")
            .Should()
            .Be(1, "System.Net.Mail no agrega un segundo Message-ID");
        first
            .Split('\n')
            .Single(l => l.StartsWith("Message-ID:", StringComparison.Ordinal))
            .Should()
            .NotContain("cliente");
    }

    [Fact]
    public void Sin_CommunicationId_no_se_fija_Message_ID()
    {
        WriteEml(Message(null)).Should().NotContain("Message-ID:");
    }

    // ── Timeout ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Servidor_SMTP_que_no_responde_produce_TimeoutException_Transient_dentro_del_timeout()
    {
        // Acepta la conexión TCP y nunca envía el saludo SMTP.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = listener.AcceptTcpClientAsync();

        var started = DateTime.UtcNow;
        var act = () =>
            new SmtpEmailSender().SendAsync(
                Message(Guid.NewGuid()),
                Settings(TimeSpan.FromSeconds(1), port)
            );

        var thrown = await act.Should().ThrowAsync<TimeoutException>();
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(10));
        CommunicationFailureClassifier
            .Classify(thrown.Which)
            .Should()
            .Be(CommunicationFailureCategory.Transient);
        (await accept).Dispose();
    }

    [Theory]
    [InlineData(null, 30)]
    [InlineData(0, 30)]
    [InlineData(1, 5)]
    [InlineData(45, 45)]
    [InlineData(600, 120)]
    public void Timeout_configurado_se_acota_y_siempre_queda_bajo_el_lease(
        int? configured,
        int expectedSeconds
    )
    {
        var timeout = CommunicationDeliveryTiming.ResolveSmtpTimeout(configured);

        timeout.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
        (timeout + CommunicationDeliveryTiming.WorkMargin)
            .Should()
            .BeLessThan(CommunicationDeliveryTiming.Lease);
    }

    // ── Clasificación ─────────────────────────────────────────────────────────────────────

    public static TheoryData<Exception, CommunicationFailureCategory> Failures() =>
        new()
        {
            { new TimeoutException(), CommunicationFailureCategory.Transient },
            {
                new SmtpException(SmtpStatusCode.ServiceNotAvailable),
                CommunicationFailureCategory.Transient
            },
            {
                new SmtpException(SmtpStatusCode.MailboxBusy),
                CommunicationFailureCategory.Transient
            },
            {
                new SmtpException(
                    "conexión",
                    new SocketException((int)SocketError.ConnectionRefused)
                ),
                CommunicationFailureCategory.Transient
            },
            { new IOException("reset"), CommunicationFailureCategory.Transient },
            {
                new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, "x@test.com"),
                CommunicationFailureCategory.Permanent
            },
            {
                new SmtpFailedRecipientException(
                    SmtpStatusCode.MailboxNameNotAllowed,
                    "x@test.com"
                ),
                CommunicationFailureCategory.Permanent
            },
            {
                new SmtpFailedRecipientsException(
                    "varios",
                    [
                        new SmtpFailedRecipientException(
                            SmtpStatusCode.MailboxUnavailable,
                            "x@test.com"
                        ),
                    ]
                ),
                CommunicationFailureCategory.Permanent
            },
            {
                new SmtpFailedRecipientsException(
                    "varios",
                    [new SmtpFailedRecipientException(SmtpStatusCode.MailboxBusy, "x@test.com")]
                ),
                CommunicationFailureCategory.Transient
            },
            {
                new SmtpException(SmtpStatusCode.ExceededStorageAllocation),
                CommunicationFailureCategory.Permanent
            },
            { new FormatException(), CommunicationFailureCategory.Permanent },
            {
                new SmtpException((SmtpStatusCode)535, "auth"),
                CommunicationFailureCategory.Configuration
            },
            {
                new SmtpException(SmtpStatusCode.MustIssueStartTlsFirst),
                CommunicationFailureCategory.Configuration
            },
            {
                new SmtpException(SmtpStatusCode.ClientNotPermitted),
                CommunicationFailureCategory.Configuration
            },
            {
                new SmtpException("conexión", new SocketException((int)SocketError.HostNotFound)),
                CommunicationFailureCategory.Configuration
            },
            { new SmtpException(SmtpStatusCode.SyntaxError), CommunicationFailureCategory.Unknown },
            {
                new SmtpException(SmtpStatusCode.GeneralFailure),
                CommunicationFailureCategory.Unknown
            },
            { new InvalidOperationException(), CommunicationFailureCategory.Unknown },
        };

    [Theory]
    [MemberData(nameof(Failures))]
    public void Clasifica_con_informacion_estructurada(
        Exception exception,
        CommunicationFailureCategory expected
    )
    {
        CommunicationFailureClassifier.Classify(exception).Should().Be(expected);
    }

    private static string WriteEml(EmailMessage message)
    {
        var dir = Directory.CreateTempSubdirectory("zh-eml-").FullName;
        try
        {
            using var mail = SmtpEmailSender.BuildMailMessage(message, Settings());
            using var client = new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = dir,
            };
            client.Send(mail);
            return File.ReadAllText(Directory.GetFiles(dir).Single());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static int CountOccurrences(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length)
        / value.Length;
}
