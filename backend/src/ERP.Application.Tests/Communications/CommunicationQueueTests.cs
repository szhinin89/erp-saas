using ERP.Application.Common;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — MaxRetries se copia al ENCOLAR desde el perfil resuelto;
/// la idempotencia de encolado no cambia.
/// </summary>
public sealed class CommunicationQueueTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICommunicationOutboxRepository> Outbox { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ICommunicationSettingsResolver> Settings { get; } = new();
        public List<CommunicationOutbox> Added { get; } = new();

        public Fixture(int configuredMaxRetries = 7)
        {
            Settings
                .Setup(s => s.ResolveEmailAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommunicationEmailSettings(true, "smtp", 587, null, null, "s@e.com", null, true, null, configuredMaxRetries, "es"));
            Outbox
                .Setup(o => o.AddAsync(It.IsAny<CommunicationOutbox>(), It.IsAny<CancellationToken>()))
                .Callback<CommunicationOutbox, CancellationToken>((c, _) => Added.Add(c))
                .Returns(Task.CompletedTask);
        }

        public CommunicationQueue Build()
        {
            var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
            var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);
            var branch = Mock.Of<ICurrentBranch>(b => b.HasBranchContext == false);
            var user = Mock.Of<ICurrentUser>(u => u.UserId == Guid.NewGuid());
            return new CommunicationQueue(Outbox.Object, UnitOfWork.Object, tenant, company, branch, user, Settings.Object);
        }
    }

    private static QueueEmailRequest Request(int? maxRetries = null, string? key = "invoice:1") =>
        new("SALES_INVOICE_AUTHORIZED", "Cliente", "cliente@test.com", "Factura", "<p>x</p>", null,
            MaxRetries: maxRetries, IdempotencyKey: key, SaveImmediately: false);

    [Fact]
    public async Task Sin_MaxRetries_explicito_copia_el_de_la_configuracion_resuelta()
    {
        var f = new Fixture(configuredMaxRetries: 7);

        await f.Build().QueueEmailAsync(Request());

        f.Added.Should().ContainSingle().Which.MaxRetries.Should().Be(7);
    }

    [Fact]
    public async Task MaxRetries_explicito_prevalece_y_no_consulta_configuracion()
    {
        var f = new Fixture();

        await f.Build().QueueEmailAsync(Request(maxRetries: 2));

        f.Added.Single().MaxRetries.Should().Be(2);
        f.Settings.Verify(s => s.ResolveEmailAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Clave_ya_encolada_devuelve_la_existente_sin_crear_otra()
    {
        var f = new Fixture();
        var existing = CommunicationOutbox.CreateEmail(TenantId, CompanyId, null, "P", null, "c@test.com", "s", "<p>x</p>", null,
            Domain.Modules.Communications.Enums.CommunicationPriority.Normal, null, 3, null, null, "invoice:1", Guid.Empty);
        f.Outbox
            .Setup(o => o.GetByIdempotencyKeyAsync(TenantId, CompanyId, "invoice:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await f.Build().QueueEmailAsync(Request());

        result.Id.Should().Be(existing.Id);
        result.WasAlreadyQueued.Should().BeTrue();
        f.Added.Should().BeEmpty();
    }
}
