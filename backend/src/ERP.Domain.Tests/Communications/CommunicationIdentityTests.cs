using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — identidad lógica (ADR-039 D8) e invariantes de alcance (D3).
/// </summary>
public sealed class CommunicationIdentityTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();

    private static CommunicationIdentity Invoice(
        Guid? sourceId = null,
        CommunicationRecipientRole role = CommunicationRecipientRole.Customer,
        Guid? companyId = null
    ) =>
        CommunicationIdentity.For(
            CommunicationScope.Company(TenantId, companyId ?? CompanyId),
            CommunicationPurposes.SalesInvoiceAuthorized,
            CommunicationChannel.Email,
            new CommunicationSource("Sales", "SalesInvoice", sourceId ?? InvoiceId),
            role
        );

    private static CommunicationOutbox Email(CommunicationIdentity identity, string email, string subject, string body) =>
        CommunicationOutbox.CreateEmail(identity, null, email, new CommunicationTemplateUsage(identity.Purpose, 1, CommunicationTemplateSource.Default), subject, body, null, CommunicationPriority.Normal, null, 3, Guid.Empty);

    // ── Identidad ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Misma_request_logica_produce_la_misma_identidad_determinista_y_acotada()
    {
        var a = Invoice();
        var b = Invoice();

        b.Key.Should().Be(a.Key);
        a.Key.Should().StartWith("cid:v1:").And.HaveLength(71).And.MatchRegex("^cid:v1:[0-9a-f]{64}$");
        a.Key.Length.Should().BeLessThanOrEqualTo(CommunicationOutbox.IdempotencyKeyMaxLen);
    }

    [Fact]
    public void Email_asunto_y_cuerpo_no_forman_parte_de_la_identidad()
    {
        var identity = Invoice();

        var first = Email(identity, "cliente@test.com", "Factura 001", "<p>uno</p>");
        var changed = Email(identity, "nuevo-email@otro.com", "Otro asunto", "<p>otro cuerpo</p>");

        changed.IdempotencyKey.Should().Be(first.IdempotencyKey);
    }

    [Fact]
    public void Cambiar_proposito_origen_rol_o_empresa_cambia_la_identidad()
    {
        var baseKey = Invoice().Key;

        Invoice(sourceId: Guid.NewGuid()).Key.Should().NotBe(baseKey, "otro SourceId");
        Invoice(role: CommunicationRecipientRole.CompanyCopy).Key.Should().NotBe(baseKey, "otro RecipientRole");
        Invoice(companyId: Guid.NewGuid()).Key.Should().NotBe(baseKey, "otro alcance");

        var otherPurpose = CommunicationIdentity.For(
            CommunicationScope.System,
            CommunicationPurposes.PasswordReset,
            CommunicationChannel.Email,
            new CommunicationSource("Sales", "SalesInvoice", InvoiceId),
            CommunicationRecipientRole.Customer
        );
        otherPurpose.Key.Should().NotBe(baseKey, "otro Purpose");
    }

    [Fact]
    public void Reenvio_manual_tiene_identidad_nueva_y_explicita_por_secuencia()
    {
        var original = Invoice();

        var first = original.ForResend(1);
        var second = original.ForResend(2);

        first.Key.Should().NotBe(original.Key);
        second.Key.Should().NotBe(first.Key);
        original.ForResend(1).Key.Should().Be(first.Key, "el mismo reenvío es idempotente");
        first.ResendSequence.Should().Be(1);
    }

    [Fact]
    public void Proposito_sin_reenvio_manual_lo_rechaza()
    {
        var reset = CommunicationIdentity.For(
            CommunicationScope.System,
            CommunicationPurposes.PasswordReset,
            CommunicationChannel.Email,
            new CommunicationSource("Authentication", "PasswordReset", Guid.NewGuid()),
            CommunicationRecipientRole.User
        );

        var act = () => reset.ForResend(1);

        act.Should().Throw<ERP.Domain.Exceptions.DomainRuleViolationException>();
    }

    // ── Invariantes de alcance y registro de propósitos ─────────────────────────────────

    [Fact]
    public void Scope_Company_exige_tenant_y_empresa()
    {
        var sinTenant = () => CommunicationScope.Company(Guid.Empty, CompanyId);
        var sinEmpresa = () => CommunicationScope.Company(TenantId, Guid.Empty);

        sinTenant.Should().Throw<ArgumentException>();
        sinEmpresa.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(CommunicationScopeKind.System, true, false)]
    [InlineData(CommunicationScopeKind.System, false, true)]
    [InlineData(CommunicationScopeKind.Company, true, false)]
    [InlineData(CommunicationScopeKind.Company, false, true)]
    public void Combinacion_invalida_de_alcance_falla(CommunicationScopeKind kind, bool withTenant, bool withCompany)
    {
        var act = () => CommunicationScope.From(kind, withTenant ? TenantId : null, withCompany ? CompanyId : null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Proposito_con_alcance_o_canal_no_permitido_falla_antes_de_persistir()
    {
        var invoiceAsSystem = () => CommunicationIdentity.For(
            CommunicationScope.System,
            CommunicationPurposes.SalesInvoiceAuthorized,
            CommunicationChannel.Email,
            new CommunicationSource("Sales", "SalesInvoice", InvoiceId),
            CommunicationRecipientRole.Customer
        );
        var resetAsCompany = () => CommunicationIdentity.For(
            CommunicationScope.Company(TenantId, CompanyId),
            CommunicationPurposes.PasswordReset,
            CommunicationChannel.Email,
            new CommunicationSource("Authentication", "PasswordReset", Guid.NewGuid()),
            CommunicationRecipientRole.User
        );
        var invoiceBySms = () => CommunicationIdentity.For(
            CommunicationScope.Company(TenantId, CompanyId),
            CommunicationPurposes.SalesInvoiceAuthorized,
            CommunicationChannel.Sms,
            new CommunicationSource("Sales", "SalesInvoice", InvoiceId),
            CommunicationRecipientRole.Customer
        );
        var unknownPurpose = () => CommunicationIdentity.For(
            CommunicationScope.Company(TenantId, CompanyId),
            "NO_EXISTE",
            CommunicationChannel.Email,
            new CommunicationSource("Sales", "SalesInvoice", InvoiceId),
            CommunicationRecipientRole.Customer
        );

        invoiceAsSystem.Should().Throw<ArgumentException>();
        resetAsCompany.Should().Throw<ArgumentException>();
        invoiceBySms.Should().Throw<ArgumentException>();
        unknownPurpose.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Registro_declara_metadata_estable_de_cada_proposito()
    {
        var invoice = CommunicationPurposes.Get(CommunicationPurposes.SalesInvoiceAuthorized);
        invoice.ScopeKind.Should().Be(CommunicationScopeKind.Company);
        invoice.IsSensitive.Should().BeFalse();
        invoice.AllowsManualResend.Should().BeTrue();

        var reset = CommunicationPurposes.Get(CommunicationPurposes.PasswordReset);
        reset.ScopeKind.Should().Be(CommunicationScopeKind.System);
        reset.IsSensitive.Should().BeTrue();
        reset.AllowsManualResend.Should().BeFalse();

        CommunicationPurposes.All.Should().OnlyContain(p => p.Channels.Contains(CommunicationChannel.Email));
    }

    [Fact]
    public void Origen_invalido_falla()
    {
        var sinId = () => new CommunicationSource("Sales", "SalesInvoice", Guid.Empty);
        var sinModulo = () => new CommunicationSource(" ", "SalesInvoice", Guid.NewGuid());
        var separador = () => new CommunicationSource("Sa|les", "SalesInvoice", Guid.NewGuid());

        sinId.Should().Throw<ArgumentException>();
        sinModulo.Should().Throw<ArgumentException>();
        separador.Should().Throw<ArgumentException>();
    }
}
