using System.Reflection;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Common;
using ERP.Domain.Modules.Communications.Entities;
using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 (ADR-039 D3/D6/D8) — fronteras de Communications. Ratchet con
/// baseline = 0: ninguna excepción heredada.
/// <list type="bullet">
/// <item>El alcance opcional (<see cref="IOptionalCompanyScopeEntity"/>) es exclusivo de Communications
/// y su tenant/empresa no se reasignan.</item>
/// <item>Nadie arma la IdempotencyKey: no hay parámetro ni propiedad pública que la reciba.</item>
/// <item>La cola no infiere el tenant del contexto (el alcance llega en la request).</item>
/// <item>Solo Communications escribe la outbox (entidad, repositorio, store).</item>
/// <item>SMTP e <see cref="IEmailSender"/> solo dentro de Communications.</item>
/// </list>
/// </summary>
public sealed class CommunicationsBoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(CommunicationOutbox).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ICommunicationQueue).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(ERP.Infrastructure.Persistence.ErpDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(ERP.API.Controllers.CommunicationsEmailSettingsController).Assembly;

    private static IReadOnlyList<string> Failing(PredicateList predicates) =>
        predicates.GetTypes().Select(t => t.FullName!).Where(n => !n.Contains('<')).ToList();

    private static bool StartsWithAny(string? name, params string[] prefixes) =>
        name is not null && prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    [Fact]
    public void Alcance_opcional_es_exclusivo_de_Communications_y_sin_setters_publicos()
    {
        var implementors = DomainAssembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IOptionalCompanyScopeEntity).IsAssignableFrom(t))
            .ToList();

        implementors.Select(t => t.Name).Should().BeEquivalentTo(
            nameof(CommunicationOutbox),
            nameof(CommunicationOutboxAttachment),
            nameof(CommunicationDeliveryAttempt)
        );

        implementors
            .SelectMany(t => new[] { t.GetProperty("TenantId"), t.GetProperty("CompanyId") })
            .Where(p => p?.SetMethod is { IsPublic: true })
            .Should().BeEmpty("tenant/empresa de una comunicación no se reasignan");

        implementors.Should().OnlyContain(
            t => !typeof(ITenantScopedEntity).IsAssignableFrom(t),
            "una comunicación System no tiene tenant: no puede ser ITenantScopedEntity ni usar un centinela"
        );
    }

    [Fact]
    public void Ninguna_API_recibe_una_IdempotencyKey_armada_por_el_caller()
    {
        var communicationsTypes = DomainAssembly.GetTypes()
            .Concat(ApplicationAssembly.GetTypes())
            .Where(t => StartsWithAny(t.Namespace, "ERP.Domain.Modules.Communications", "ERP.Application.Modules.Communications"))
            // Proyecciones de lectura (*ItemDto) exponen la clave ya persistida; no la reciben del caller.
            .Where(t => !t.Name.EndsWith("ItemDto", StringComparison.Ordinal))
            .ToList();

        var keyParameters = communicationsTypes
            .SelectMany(t => t.GetConstructors().Cast<MethodBase>().Concat(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)))
            .Where(m => m.IsPublic)
            .SelectMany(m => m.GetParameters().Select(p => new { Member = $"{m.DeclaringType!.Name}.{m.Name}", p.Name }))
            .Where(p => string.Equals(p.Name, "idempotencyKey", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Member)
            .ToList();

        keyParameters.Should().BeEmpty("la identidad la construye solo CommunicationIdentity (ADR-039 D8)");
        typeof(CommunicationRequest).GetProperty("IdempotencyKey").Should().BeNull();
        typeof(CommunicationOutbox).GetProperty(nameof(CommunicationOutbox.IdempotencyKey))!.SetMethod!.IsPublic.Should().BeFalse();
    }

    [Fact]
    public void La_cola_no_infiere_el_tenant_del_contexto()
    {
        Failing(
                Types.InAssembly(ApplicationAssembly)
                    .That().HaveName(nameof(CommunicationQueue))
                    .And().HaveDependencyOn(typeof(ERP.Application.Common.ICurrentTenant).FullName!)
            )
            .Should().BeEmpty("el alcance llega explícito en CommunicationRequest");
    }

    [Fact]
    public void Solo_Communications_escribe_la_outbox()
    {
        string[] outboxTypes =
        [
            typeof(CommunicationOutbox).FullName!,
            typeof(ERP.Domain.Modules.Communications.Interfaces.ICommunicationOutboxRepository).FullName!,
            typeof(ERP.Infrastructure.Communications.CommunicationOutboxDeliveryStore).FullName!,
        ];

        var offenders = new[] { ApplicationAssembly, InfrastructureAssembly, ApiAssembly }
            .SelectMany(assembly => outboxTypes.SelectMany(type => Failing(Types.InAssembly(assembly).That().HaveDependencyOn(type))))
            .Where(name => !StartsWithAny(
                name,
                "ERP.Application.Modules.Communications.",
                "ERP.Infrastructure.Communications.",
                "ERP.Infrastructure.Persistence.ErpDbContext",
                "ERP.Infrastructure.Persistence.Configurations.Communications.",
                "ERP.Infrastructure.Persistence.Repositories.Communications.",
                "ERP.Infrastructure.Migrations.",
                "ERP.Infrastructure.DependencyInjection"
            ))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty("los módulos encolan por ICommunicationQueue; nunca escriben la outbox (ADR-039 D6)");
    }

    [Fact]
    public void SMTP_e_IEmailSender_solo_dentro_de_Communications()
    {
        var mailOffenders = new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly }
            .SelectMany(assembly => Failing(Types.InAssembly(assembly).That().HaveDependencyOn("System.Net.Mail")))
            .Where(name => !StartsWithAny(name, "ERP.Application.Modules.Communications.", "ERP.Infrastructure.Communications."))
            .ToList();

        var smtpClientOffenders = new[] { ApplicationAssembly, InfrastructureAssembly, ApiAssembly }
            .SelectMany(assembly => Failing(Types.InAssembly(assembly).That().HaveDependencyOn("System.Net.Mail.SmtpClient")))
            .Where(name => !StartsWithAny(name, "ERP.Infrastructure.Communications.SmtpEmailSender"))
            .ToList();

        var senderOffenders = new[] { ApplicationAssembly, InfrastructureAssembly, ApiAssembly }
            .SelectMany(assembly => Failing(Types.InAssembly(assembly).That().HaveDependencyOn(typeof(IEmailSender).FullName!)))
            .Where(name => !StartsWithAny(
                name,
                "ERP.Application.Modules.Communications.",
                "ERP.Infrastructure.Communications.",
                "ERP.Infrastructure.DependencyInjection"
            ))
            .ToList();

        mailOffenders.Should().BeEmpty("System.Net.Mail solo en Communications");
        smtpClientOffenders.Should().BeEmpty("SmtpClient solo en SmtpEmailSender (ADR-039 D17)");
        senderOffenders.Should().BeEmpty("ningún módulo llama SMTP ni IEmailSender (ADR-039 D6)");
    }
}
