using ERP.Application.MasterData.UseCases.GetCompanyBpSalesSettings;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>ADR-033, Fase 3d.</summary>
public sealed class GetCompanyBpSalesSettingsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Sin_fila_existente_devuelve_defaults_con_HasCustomConfiguration_false()
    {
        var repo = new Mock<ICompanyBpSalesSettingsRepository>();
        repo.Setup(r => r.GetByBusinessPartnerAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyBpSalesSettings?)null);

        var handler = new GetCompanyBpSalesSettingsHandler(repo.Object);
        var result = await handler.Handle(
            new GetCompanyBpSalesSettingsQuery(CustomerId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasCustomConfiguration.Should().BeFalse();
        result.Value.PaymentTermId.Should().BeNull();
        result.Value.BusinessPartnerId.Should().Be(CustomerId);
    }

    [Fact]
    public async Task Con_fila_existente_devuelve_el_valor_real()
    {
        var paymentTermId = Guid.NewGuid();
        var settings = CompanyBpSalesSettings.Create(
            TenantId, CompanyId, CustomerId, paymentTermId, UserId
        );
        var repo = new Mock<ICompanyBpSalesSettingsRepository>();
        repo.Setup(r => r.GetByBusinessPartnerAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var handler = new GetCompanyBpSalesSettingsHandler(repo.Object);
        var result = await handler.Handle(
            new GetCompanyBpSalesSettingsQuery(CustomerId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasCustomConfiguration.Should().BeTrue();
        result.Value.PaymentTermId.Should().Be(paymentTermId);
    }
}
