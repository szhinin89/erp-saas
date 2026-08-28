using ERP.Domain.Modules.Payables.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Payables;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — pruebas puras de dominio de
/// <see cref="SupplierPaymentSequence.CaptureAndIncrement"/>, mismo alcance que
/// <c>PurchaseReturnSequenceTests</c> (comportamiento en memoria; persistencia/advisory locks son
/// responsabilidad de <c>ISupplierPaymentSequenceRepository</c>).
/// </summary>
public sealed class SupplierPaymentSequenceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public void Create_arranca_en_1_sin_prefijo()
    {
        var sequence = SupplierPaymentSequence.Create(TenantId, CompanyId);

        sequence.CurrentSeq.Should().Be(1);
        sequence.Prefix.Should().BeNull();
        sequence.TenantId.Should().Be(TenantId);
        sequence.CompanyId.Should().Be(CompanyId);
    }

    [Fact]
    public void Create_rechaza_companyId_vacio()
    {
        var act = () => SupplierPaymentSequence.Create(TenantId, Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CaptureAndIncrement_sin_prefijo_devuelve_D8()
    {
        var sequence = SupplierPaymentSequence.Create(TenantId, CompanyId);

        sequence.CaptureAndIncrement().Should().Be("00000001");
        sequence.CurrentSeq.Should().Be(2);
    }

    [Fact]
    public void CaptureAndIncrement_con_prefijo_antepone_prefijo_y_guion()
    {
        var sequence = SupplierPaymentSequence.Create(TenantId, CompanyId, "PP");

        sequence.CaptureAndIncrement().Should().Be("PP-00000001");
    }

    [Fact]
    public void CaptureAndIncrement_multiples_llamadas_mantienen_incremento_estrictamente_secuencial()
    {
        var sequence = SupplierPaymentSequence.Create(TenantId, CompanyId);
        var captured = new List<string>();

        for (var i = 0; i < 5; i++)
            captured.Add(sequence.CaptureAndIncrement());

        captured.Should().Equal("00000001", "00000002", "00000003", "00000004", "00000005");
        sequence.CurrentSeq.Should().Be(6);
    }
}
