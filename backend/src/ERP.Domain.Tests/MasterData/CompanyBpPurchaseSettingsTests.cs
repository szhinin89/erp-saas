using ERP.Domain.MasterData.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.MasterData;

/// <summary>ADR-033, Fase 3a — default de condición de pago de proveedor por empresa.</summary>
public sealed class CompanyBpPurchaseSettingsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact]
    public void Create_con_datos_validos_asigna_propiedades()
    {
        var entity = CompanyBpPurchaseSettings.Create(
            TenantId, CompanyId, SupplierId, PaymentTermId, ActorId
        );

        entity.TenantId.Should().Be(TenantId);
        entity.CompanyId.Should().Be(CompanyId);
        entity.BusinessPartnerId.Should().Be(SupplierId);
        entity.PaymentTermId.Should().Be(PaymentTermId);
        entity.CreatedBy.Should().Be(ActorId);
    }

    [Fact]
    public void Create_sin_PaymentTermId_es_valido_significa_sin_default_configurado()
    {
        var entity = CompanyBpPurchaseSettings.Create(
            TenantId, CompanyId, SupplierId, paymentTermId: null, ActorId
        );

        entity.PaymentTermId.Should().BeNull();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Create_rechaza_ids_vacios(bool emptyTenant, bool emptyCompany, bool emptySupplier)
    {
        var tenantId = emptyTenant ? Guid.Empty : TenantId;
        var companyId = emptyCompany ? Guid.Empty : CompanyId;
        var supplierId = emptySupplier ? Guid.Empty : SupplierId;

        var act = () => CompanyBpPurchaseSettings.Create(
            tenantId, companyId, supplierId, PaymentTermId, ActorId
        );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetPaymentTerm_actualiza_el_default_y_UpdatedBy()
    {
        var entity = CompanyBpPurchaseSettings.Create(
            TenantId, CompanyId, SupplierId, PaymentTermId, ActorId
        );
        var newPaymentTermId = Guid.NewGuid();
        var updatedBy = Guid.NewGuid();

        entity.SetPaymentTerm(newPaymentTermId, updatedBy);

        entity.PaymentTermId.Should().Be(newPaymentTermId);
        entity.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void SetPaymentTerm_con_null_limpia_el_default()
    {
        var entity = CompanyBpPurchaseSettings.Create(
            TenantId, CompanyId, SupplierId, PaymentTermId, ActorId
        );

        entity.SetPaymentTerm(null, ActorId);

        entity.PaymentTermId.Should().BeNull();
    }
}
