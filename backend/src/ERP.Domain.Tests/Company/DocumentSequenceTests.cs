using ERP.Domain.Modules.Company.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Company;

/// <summary>
/// DOCUMENT-SEQUENCES-CONFIG-03 — reglas de dominio de <see cref="DocumentSequence.ConfigureNextNumber"/>.
/// No cubre concurrencia/persistencia (eso vive en ERP.Infrastructure.Tests — Testcontainers real);
/// esta suite es puramente de invariantes en memoria.
/// </summary>
public sealed class DocumentSequenceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();

    private static DocumentSequence CreateUnused() =>
        DocumentSequence.Create(TenantId, CompanyId, EmissionPointId, "07");

    [Fact]
    public void Create_arranca_en_1_y_HasBeenUsed_false()
    {
        var sequence = CreateUnused();

        sequence.CurrentSeq.Should().Be(1);
        sequence.HasBeenUsed.Should().BeFalse();
    }

    [Fact]
    public void ConfigureNextNumber_en_secuencia_nunca_usada_fija_CurrentSeq_sin_marcarla_usada()
    {
        var sequence = CreateUnused();

        sequence.ConfigureNextNumber(850);

        sequence.CurrentSeq.Should().Be(850);
        sequence.HasBeenUsed.Should().BeFalse(
            "configurar el número inicial no es una captura real — debe poder reconfigurarse de nuevo"
        );
    }

    [Fact]
    public void ConfigureNextNumber_puede_reconfigurarse_varias_veces_mientras_no_haya_captura_real()
    {
        var sequence = CreateUnused();

        sequence.ConfigureNextNumber(850);
        sequence.ConfigureNextNumber(900); // corrección antes del primer uso real — debe permitirse

        sequence.CurrentSeq.Should().Be(900);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void ConfigureNextNumber_rechaza_valores_cero_o_negativos(int invalidValue)
    {
        var sequence = CreateUnused();

        var act = () => sequence.ConfigureNextNumber(invalidValue);

        act.Should().Throw<ArgumentOutOfRangeException>();
        sequence.CurrentSeq.Should().Be(1, "un intento inválido no debe mutar el estado existente");
    }

    [Fact]
    public void ConfigureNextNumber_rechaza_valores_que_no_caben_en_9_digitos()
    {
        var sequence = CreateUnused();

        var act = () => sequence.ConfigureNextNumber(DocumentSequence.MaxSequentialValue + 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ConfigureNextNumber_acepta_el_maximo_exacto_de_9_digitos()
    {
        var sequence = CreateUnused();

        sequence.ConfigureNextNumber(DocumentSequence.MaxSequentialValue);

        sequence.CurrentSeq.Should().Be(DocumentSequence.MaxSequentialValue);
    }

    [Fact]
    public void CaptureAndIncrement_marca_HasBeenUsed_true_y_ConfigureNextNumber_posterior_se_rechaza()
    {
        var sequence = CreateUnused();
        sequence.ConfigureNextNumber(850);

        var captured = sequence.CaptureAndIncrement();

        captured.Should().Be("000000850");
        sequence.HasBeenUsed.Should().BeTrue();

        var act = () => sequence.ConfigureNextNumber(900);
        act.Should()
            .Throw<InvalidOperationException>(
                "una vez que hubo una captura real, el ajuste libre queda fuera de esta fase"
            );
        sequence.CurrentSeq.Should().Be(851, "el intento rechazado no debe mutar el estado");
    }

    [Fact]
    public void CaptureAndIncrement_sin_configuracion_previa_sigue_entregando_000000001()
    {
        var sequence = CreateUnused();

        var captured = sequence.CaptureAndIncrement();

        captured.Should().Be("000000001");
        sequence.HasBeenUsed.Should().BeTrue();
    }
}
