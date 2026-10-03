using ERP.Application.Common;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Interfaces;
using ERP.Domain.Modules.Communications.ValueObjects;
using FluentAssertions;
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
                .Setup(s => s.ResolveEmailAsync(It.IsAny<CommunicationScope>(), It.IsAny<CancellationToken>()))
                .Callback<CommunicationScope, CancellationToken>((scope, _) => ResolvedScopes.Add(scope))
                .ReturnsAsync(new CommunicationEmailSettings(true, "smtp", 587, null, null, "s@e.com", null, true, null, configuredMaxRetries, "es"));
            Outbox
                .Setup(o => o.EnqueueAsync(It.IsAny<CommunicationOutbox>(), It.IsAny<CancellationToken>()))
                .Callback<CommunicationOutbox, CancellationToken>((c, _) => Enqueued.Add(c))
                .ReturnsAsync((CommunicationOutbox c, CancellationToken _) => new CommunicationEnqueueResult(c.Id, Created: !alreadyQueued));
            CurrentCompany.Setup(c => c.HasCompanyContext).Returns(false);
        }

        public CommunicationQueue Build() =>
            new(Outbox.Object, CurrentCompany.Object, Mock.Of<ICurrentUser>(u => u.UserId == Guid.NewGuid()), Settings.Object);
    }

    private static CommunicationRequest Request(CommunicationScope? scope = null, int? maxRetries = null) =>
        new(
            scope ?? CommunicationScope.Company(TenantId, CompanyId),
            CommunicationPurposes.SalesInvoiceAuthorized,
            new CommunicationSource("Sales", "SalesInvoice", Guid.NewGuid()),
            CommunicationRecipientRole.Customer,
            "Cliente",
            "cliente@test.com",
            "Factura",
            "<p>x</p>",
            null,
            MaxRetries: maxRetries
        );

    [Fact]
    public async Task Usa_el_alcance_explicito_y_la_identidad_central()
    {
        var f = new Fixture();
        var request = Request();

        await f.Build().EnqueueAsync(request);

        var created = f.Enqueued.Should().ContainSingle().Subject;
        created.TenantId.Should().Be(TenantId);
        created.CompanyId.Should().Be(CompanyId);
        created.IdempotencyKey.Should().Be(
            CommunicationIdentity.For(request.Scope, request.Purpose, request.Channel, request.Source, request.RecipientRole).Key
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
        f.Settings.Verify(s => s.ResolveEmailAsync(It.IsAny<CommunicationScope>(), It.IsAny<CancellationToken>()), Times.Never);
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
            "Recupera tu acceso",
            null,
            "Instrucciones"
        );

        await f.Build().EnqueueAsync(request);

        var created = f.Enqueued.Single();
        created.ScopeKind.Should().Be(CommunicationScopeKind.System);
        created.TenantId.Should().BeNull();
        created.CompanyId.Should().BeNull("System no hereda la empresa del contexto");
        f.ResolvedScopes.Should().Equal(CommunicationScope.System);
    }
}
