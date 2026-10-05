using ERP.Application.Common;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Communications.Templates;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Interfaces;
using ERP.Domain.Modules.Communications.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — la cola usa el alcance EXPLÍCITO de la request (sin inferirlo del
/// contexto), arma la identidad con CommunicationIdentity, copia MaxRetries del perfil de ese mismo
/// alcance y delega la idempotencia en el repositorio (PostgreSQL).
/// </summary>
public sealed class CommunicationQueueTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICommunicationOutboxRepository> Outbox { get; } = new();
        public Mock<ICommunicationSettingsResolver> Settings { get; } = new();
        public Mock<ICurrentCompany> CurrentCompany { get; } = new();
        public List<CommunicationOutbox> Enqueued { get; } = new();
        public List<CommunicationScope> ResolvedScopes { get; } = new();

        public Fixture(int configuredMaxRetries = 7, bool alreadyQueued = false)
        {
            Settings
                .Setup(s =>
                    s.ResolveEmailAsync(
                        It.IsAny<CommunicationScope>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<CommunicationScope, CancellationToken>(
                    (scope, _) => ResolvedScopes.Add(scope)
                )
                .ReturnsAsync(
                    new CommunicationEmailSettings(
                        true,
                        "smtp",
                        587,
                        null,
                        null,
                        "s@e.com",
                        null,
                        true,
                        null,
                        configuredMaxRetries,
                        "es"
                    )
                );
            Outbox
                .Setup(o =>
                    o.EnqueueAsync(It.IsAny<CommunicationOutbox>(), It.IsAny<CancellationToken>())
                )
                .Callback<CommunicationOutbox, CancellationToken>((c, _) => Enqueued.Add(c))
                .ReturnsAsync(
                    (CommunicationOutbox c, CancellationToken _) =>
                        new CommunicationEnqueueResult(c.Id, Created: !alreadyQueued)
                );
            CurrentCompany.Setup(c => c.HasCompanyContext).Returns(false);
        }

        public Mock<ICommunicationTemplateResolver> Templates { get; } = new();

        public CommunicationQueue Build()
        {
            Templates
                .Setup(t =>
                    t.ResolveAsync(
                        It.IsAny<CommunicationScope>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    (CommunicationScope _, string key, CancellationToken _) =>
                        key == CommunicationPurposes.PasswordReset
                            ? Result<CommunicationTemplateDefinition>.Success(
                                StructuralPasswordResetTemplate.Definition
                            )
                            : Result<CommunicationTemplateDefinition>.Success(
                                CommunicationDefaultTemplates.SalesInvoiceAuthorizedV1
                            )
                );
            return new(
                Outbox.Object,
                CurrentCompany.Object,
                Mock.Of<ICurrentUser>(u => u.UserId == Guid.NewGuid()),
                Settings.Object,
                Templates.Object,
                NullLogger<CommunicationQueue>.Instance
            );
        }
    }

    private static readonly SalesInvoiceAuthorizedTemplateModel InvoiceModel = new(
        "Cliente",
        "001-001-000000001",
        "2108202601179214672100110010010000000011234567811",
        "100.00",
        "ZH Demo"
    );

    private static CommunicationRequest Request(
        CommunicationScope? scope = null,
        int? maxRetries = null
    ) =>
        new(
            scope ?? CommunicationScope.Company(TenantId, CompanyId),
            CommunicationPurposes.SalesInvoiceAuthorized,
            new CommunicationSource("Sales", "SalesInvoice", Guid.NewGuid()),
            CommunicationRecipientRole.Customer,
            "Cliente",
            "cliente@test.com",
            InvoiceModel,
            MaxRetries: maxRetries
        );

    // ── ZH-COMMUNICATIONS-TEMPLATES-01 ────────────────────────────────────────────────────

    [Fact]
    public async Task Renderiza_al_encolar_y_persiste_contenido_y_metadata_del_template()
    {
        var f = new Fixture();

        await f.Build().EnqueueAsync(Request());

        var created = f.Enqueued.Single();
        created.TemplateKey.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        created.TemplateVersion.Should().Be(1);
        created.TemplateSource.Should().Be(CommunicationTemplateSource.Default);
        created.Subject.Should().Be("Factura autorizada 001-001-000000001 - ZH Demo");
        created.BodyHtml.Should().Contain("<li><strong>Total:</strong> USD 100.00</li>");
        created.BodyText.Should().Contain("Emisor: ZH Demo");
    }

    [Fact]
    public async Task Fallo_de_template_registra_la_comunicacion_Failed_sin_contenido_y_expone_el_codigo()
    {
        var f = new Fixture();
        var queue = f.Build();
        f.Templates.Setup(t =>
                t.ResolveAsync(
                    It.IsAny<CommunicationScope>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CommunicationTemplateDefinition>.Failure(
                    "override inválido",
                    ApiResponseCodes.Communications.TemplateInvalid
                )
            );

        var result = await queue.EnqueueAsync(Request());

        result.FailureCode.Should().Be(ApiResponseCodes.Communications.TemplateInvalid);
        var failed = f.Enqueued.Should().ContainSingle().Subject;
        failed.Status.Should().Be(CommunicationStatus.Failed);
        failed.FailureCategory.Should().Be(CommunicationFailureCategory.Configuration);
        failed.Subject.Should().BeNull();
        failed.BodyHtml.Should().BeNull();
        failed.BodyText.Should().BeNull();
        failed.TemplateKey.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        failed.TemplatePayloadJson.Should().Contain("\"InvoiceNumber\":\"001-001-000000001\"");
        failed.LastError.Should().StartWith(ApiResponseCodes.Communications.TemplateInvalid);
    }

    [Fact]
    public async Task Fallo_de_template_de_un_proposito_sensible_no_persiste_las_variables()
    {
        var f = new Fixture();
        var queue = f.Build();
        f.Templates.Setup(t =>
                t.ResolveAsync(
                    It.IsAny<CommunicationScope>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<CommunicationTemplateDefinition>.Failure(
                    "sin template",
                    ApiResponseCodes.Communications.TemplateNotFound
                )
            );
        var request = new CommunicationRequest(
            CommunicationScope.System,
            CommunicationPurposes.PasswordReset,
            new CommunicationSource("Authentication", "PasswordReset", Guid.NewGuid()),
            CommunicationRecipientRole.User,
            null,
            "user@test.com",
            new StructuralPasswordResetTemplate.Model("Ana")
        );

        await queue.EnqueueAsync(request);

        f.Enqueued.Single()
            .TemplatePayloadJson.Should()
            .BeNull("las variables de un propósito sensible nunca se guardan en claro");
    }

    [Fact]
    public async Task Modelo_de_otro_template_que_el_proposito_se_rechaza()
    {
        var f = new Fixture();
        var request = Request() with
        {
            Template = new StructuralPasswordResetTemplate.Model("Ana"),
        };

        var result = await f.Build().EnqueueAsync(request);

        result.FailureCode.Should().Be(ApiResponseCodes.Communications.TemplateRenderFailed);
        var failed = f.Enqueued.Single();
        failed.Status.Should().Be(CommunicationStatus.Failed);
        failed
            .FailureCategory.Should()
            .Be(
                CommunicationFailureCategory.Permanent,
                "un modelo que no cumple el contrato no se corrige reintentando"
            );
    }

    [Fact]
    public async Task La_version_del_template_no_forma_parte_de_la_identidad()
    {
        var f = new Fixture();
        var request = Request();
        await f.Build().EnqueueAsync(request);

        var v2 = CommunicationDefaultTemplates.SalesInvoiceAuthorizedV1 with
        {
            Version = 2,
            SubjectTemplate = "Nuevo {{InvoiceNumber}}",
        };
        var second = new Fixture();
        var queue = second.Build();
        second
            .Templates.Setup(t =>
                t.ResolveAsync(
                    It.IsAny<CommunicationScope>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<CommunicationTemplateDefinition>.Success(v2));
        await queue.EnqueueAsync(request);

        second.Enqueued.Single().IdempotencyKey.Should().Be(f.Enqueued.Single().IdempotencyKey);
        second.Enqueued.Single().TemplateVersion.Should().Be(2);
    }

    [Fact]
    public async Task Usa_el_alcance_explicito_y_la_identidad_central()
    {
        var f = new Fixture();
        var request = Request();

        await f.Build().EnqueueAsync(request);

        var created = f.Enqueued.Should().ContainSingle().Subject;
        created.TenantId.Should().Be(TenantId);
        created.CompanyId.Should().Be(CompanyId);
        created
            .IdempotencyKey.Should()
            .Be(
                CommunicationIdentity
                    .For(
                        request.Scope,
                        request.Purpose,
                        request.Channel,
                        request.Source,
                        request.RecipientRole
                    )
                    .Key
            );
        f.ResolvedScopes.Should().Equal(request.Scope);
    }

    [Fact]
    public async Task Sin_MaxRetries_explicito_copia_el_del_perfil_del_mismo_alcance()
    {
        var f = new Fixture(configuredMaxRetries: 7);

        await f.Build().EnqueueAsync(Request());

        f.Enqueued.Single().MaxRetries.Should().Be(7);
    }

    [Fact]
    public async Task MaxRetries_explicito_prevalece_y_no_consulta_configuracion()
    {
        var f = new Fixture();

        await f.Build().EnqueueAsync(Request(maxRetries: 2));

        f.Enqueued.Single().MaxRetries.Should().Be(2);
        f.Settings.Verify(
            s => s.ResolveEmailAsync(It.IsAny<CommunicationScope>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Identidad_ya_encolada_devuelve_WasAlreadyQueued()
    {
        var f = new Fixture(alreadyQueued: true);

        var result = await f.Build().EnqueueAsync(Request());

        result.WasAlreadyQueued.Should().BeTrue();
    }

    [Fact]
    public async Task Request_Company_de_otra_empresa_que_el_contexto_autenticado_se_rechaza()
    {
        var f = new Fixture();
        f.CurrentCompany.Setup(c => c.HasCompanyContext).Returns(true);
        f.CurrentCompany.Setup(c => c.CompanyId).Returns(Guid.NewGuid());

        var act = () => f.Build().EnqueueAsync(Request());

        await act.Should().ThrowAsync<InvalidOperationException>();
        f.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_System_no_se_ve_afectada_por_el_contexto_de_empresa()
    {
        var f = new Fixture();
        f.CurrentCompany.Setup(c => c.HasCompanyContext).Returns(true);
        f.CurrentCompany.Setup(c => c.CompanyId).Returns(CompanyId);
        var request = new CommunicationRequest(
            CommunicationScope.System,
            CommunicationPurposes.PasswordReset,
            new CommunicationSource("Authentication", "PasswordReset", Guid.NewGuid()),
            CommunicationRecipientRole.User,
            null,
            "user@test.com",
            new StructuralPasswordResetTemplate.Model("Ana")
        );

        await f.Build().EnqueueAsync(request);

        var created = f.Enqueued.Single();
        created.ScopeKind.Should().Be(CommunicationScopeKind.System);
        created.TenantId.Should().BeNull();
        created.CompanyId.Should().BeNull("System no hereda la empresa del contexto");
        f.ResolvedScopes.Should().Equal(CommunicationScope.System);
    }

    // ── ZH-EDOC-COMMUNICATIONS-01 — destinatario ausente (semántica transversal) ───────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-correo")]
    [InlineData("Cliente <cliente@test.com>")]
    public async Task Destinatario_sin_correo_valido_queda_Failed_Permanent_sin_contenido_y_sin_render(
        string? email
    )
    {
        var f = new Fixture();
        var queue = f.Build();

        var result = await queue.EnqueueAsync(Request() with { RecipientEmail = email });

        result.FailureCode.Should().Be(ApiResponseCodes.Communications.RecipientMissing);
        var failed = f.Enqueued.Should().ContainSingle().Subject;
        failed.Status.Should().Be(CommunicationStatus.Failed);
        failed.FailureCategory.Should().Be(CommunicationFailureCategory.Permanent);
        failed.RecipientEmail.Should().BeNull();
        failed.Subject.Should().BeNull();
        failed
            .TemplatePayloadJson.Should()
            .Contain(
                "\"InvoiceNumber\":\"001-001-000000001\"",
                "se puede reconstruir tras corregir el contacto"
            );
        failed.LastError.Should().StartWith(ApiResponseCodes.Communications.RecipientMissing);
        f.Templates.Verify(
            t =>
                t.ResolveAsync(
                    It.IsAny<CommunicationScope>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Destinatario_ausente_conserva_la_identidad_y_los_adjuntos_por_referencia()
    {
        var f = new Fixture();
        var request = Request() with
        {
            RecipientEmail = null,
            Attachments =
            [
                new ERP.Application.Modules.Communications.DTOs.QueueCommunicationAttachmentDto(
                    CommunicationAttachmentType.RidePdf,
                    "x-RIDE.pdf",
                    "application/pdf",
                    ReferenceId: Guid.NewGuid()
                ),
            ],
        };

        await f.Build().EnqueueAsync(request);

        var failed = f.Enqueued.Single();
        failed
            .IdempotencyKey.Should()
            .Be(
                CommunicationIdentity
                    .For(
                        request.Scope,
                        request.Purpose,
                        request.Channel,
                        request.Source,
                        request.RecipientRole
                    )
                    .Key
            );
        failed
            .Attachments.Should()
            .ContainSingle(a => a.ReferenceId == request.Attachments!.Single().ReferenceId);
    }

    [Fact]
    public async Task Correo_valido_se_normaliza_en_minusculas()
    {
        var f = new Fixture();

        await f.Build().EnqueueAsync(Request() with { RecipientEmail = "  Cliente@Test.COM " });

        f.Enqueued.Single().RecipientEmail.Should().Be("cliente@test.com");
    }
}
