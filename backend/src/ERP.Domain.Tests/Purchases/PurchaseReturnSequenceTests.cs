using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// Pruebas puras de dominio de <see cref="PurchaseReturnSequence.CaptureAndIncrement"/> — cubren
/// únicamente el comportamiento del método en memoria (valor devuelto, incremento, formato,
/// invariantes). No prueban persistencia, advisory locks ni concurrencia — eso es responsabilidad
/// de <c>IPurchaseReturnSequenceRepository.CaptureNextAsync</c> (Fase 2) y de
/// <c>PurchaseReturnSequenceTransactionInteractionTests</c> (Fase 0, ya aceptado, no tocar).
/// </summary>
public sealed class PurchaseReturnSequenceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private static PurchaseReturnSequence Create() => PurchaseReturnSequence.Create(TenantId, CompanyId);

    [Fact]
    public void Create_arranca_en_1_TenantId_y_CompanyId_persistidos()
    {
        var sequence = Create();

        sequence.CurrentSeq.Should().Be(1);
        sequence.TenantId.Should().Be(TenantId);
        sequence.CompanyId.Should().Be(CompanyId);
    }

    [Fact]
    public void Create_rechaza_companyId_vacio()
    {
        var act = () => PurchaseReturnSequence.Create(TenantId, Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CaptureAndIncrement_devuelve_el_valor_actual_formateado_D8_antes_del_incremento()
    {
        var sequence = Create();

        var captured = sequence.CaptureAndIncrement();

        captured.Should().Be("00000001");
    }

    [Fact]
    public void CaptureAndIncrement_incrementa_CurrentSeq_exactamente_en_una_unidad()
    {
        var sequence = Create();

        sequence.CaptureAndIncrement();

        sequence.CurrentSeq.Should().Be(2);
    }

    [Fact]
    public void CaptureAndIncrement_dos_llamadas_consecutivas_devuelven_valores_consecutivos()
    {
        var sequence = Create();

        var first = sequence.CaptureAndIncrement();
        var second = sequence.CaptureAndIncrement();

        first.Should().Be("00000001");
        second.Should().Be("00000002");
    }

    [Fact]
    public void CaptureAndIncrement_multiples_llamadas_mantienen_incremento_estrictamente_secuencial()
    {
        var sequence = Create();
        var captured = new List<string>();

        for (var i = 0; i < 5; i++)
            captured.Add(sequence.CaptureAndIncrement());

        captured.Should().Equal("00000001", "00000002", "00000003", "00000004", "00000005");
        sequence.CurrentSeq.Should().Be(6);
    }

    [Fact]
    public void CaptureAndIncrement_no_altera_TenantId_ni_CompanyId()
    {
        var sequence = Create();

        sequence.CaptureAndIncrement();
        sequence.CaptureAndIncrement();

        sequence.TenantId.Should().Be(TenantId);
        sequence.CompanyId.Should().Be(CompanyId);
    }

    [Fact]
    public void CaptureAndIncrement_actualiza_UpdatedAt()
    {
        var sequence = Create();
        var updatedAtBefore = sequence.UpdatedAt;

        sequence.CaptureAndIncrement();

        sequence.UpdatedAt.Should().BeOnOrAfter(updatedAtBefore);
    }

    [Fact]
    public void CaptureAndIncrement_formatea_siempre_con_8_digitos_incluso_con_seq_de_multiples_digitos()
    {
        var sequence = Create();

        for (var i = 0; i < 99; i++)
            sequence.CaptureAndIncrement();

        var captured = sequence.CaptureAndIncrement();

        captured.Should().Be("00000100");
        captured.Length.Should().Be(8);
    }
}
