using ERP.Application.Common;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Interfaces;
using ERP.Domain.Modules.Communications.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Communications;

/// <summary>ZH-COMMUNICATIONS-TEMPLATES-01 — resolver único: default embebido u override de la empresa.</summary>
public sealed class CommunicationTemplateResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly CommunicationScope Company = CommunicationScope.Company(TenantId, CompanyId);
    private const string Key = CommunicationPurposes.SalesInvoiceAuthorized;

    private static CommunicationTemplate Override(string subject = "Su factura {{InvoiceNumber}}", string? html = "<p>{{CustomerName}}</p>") =>
        CommunicationTemplate.Create(TenantId, CompanyId, null, Key, "Factura", CommunicationChannel.Email, subject, html, null, "es", Guid.Empty);

    private static (CommunicationTemplateResolver Resolver, Mock<ICommunicationTemplateRepository> Repo) Build(CommunicationTemplate? companyOverride)
    {
        var repo = new Mock<ICommunicationTemplateRepository>();
        repo.Setup(r => r.GetActiveAsync(TenantId, CompanyId, CommunicationChannel.Email, Key, "es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyOverride);
        return (new CommunicationTemplateResolver(repo.Object), repo);
    }

    [Fact]
    public async Task Sin_override_usa_el_default_versionado()
    {
        var (resolver, _) = Build(null);

        var result = await resolver.ResolveAsync(Company, Key);

        result.Value.Should().Be(CommunicationDefaultTemplates.SalesInvoiceAuthorizedV1);
    }

    [Fact]
    public async Task Override_activo_de_la_empresa_se_usa_con_su_revision_y_el_contrato_del_default()
    {
        var companyOverride = Override();
        companyOverride.UpdateContent("Factura", "Su factura {{InvoiceNumber}} ({{IssuerName}})", "<p>{{CustomerName}}</p>", null, Guid.Empty);
        var (resolver, _) = Build(companyOverride);

        var result = await resolver.ResolveAsync(Company, Key);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Source.Should().Be(CommunicationTemplateSource.CompanyOverride);
        result.Value.Version.Should().Be(2, "la versión registrada de un override es su revisión");
        result.Value.SubjectTemplate.Should().Be("Su factura {{InvoiceNumber}} ({{IssuerName}})");
        result.Value.Variables.Should().BeEquivalentTo(CommunicationDefaultTemplates.SalesInvoiceAuthorizedV1.Variables);
    }

    [Fact]
    public async Task Override_invalido_falla_sin_caer_en_silencio_al_default()
    {
        var (resolver, _) = Build(Override(html: "<p>{{CustomerName}} {{Contrasena}}</p>"));

        var result = await resolver.ResolveAsync(Company, Key);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Communications.TemplateInvalid);
    }

    [Fact]
    public async Task System_nunca_consulta_overrides_de_empresa()
    {
        var (resolver, repo) = Build(Override());

        var result = await resolver.ResolveAsync(CommunicationScope.System, Key);

        result.Value!.Source.Should().Be(CommunicationTemplateSource.Default);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TemplateKey_desconocida_es_NOT_FOUND()
    {
        var (resolver, repo) = Build(null);

        var unknown = await resolver.ResolveAsync(Company, "NO_EXISTE");
        var reservedWithoutDefault = await resolver.ResolveAsync(CommunicationScope.System, CommunicationPurposes.PasswordReset);

        unknown.Code.Should().Be(ApiResponseCodes.Communications.TemplateNotFound);
        reservedWithoutDefault.Code.Should().Be(ApiResponseCodes.Communications.TemplateNotFound, "PASSWORD_RESET no tiene template productivo todavía");
        repo.VerifyNoOtherCalls();
    }
}
