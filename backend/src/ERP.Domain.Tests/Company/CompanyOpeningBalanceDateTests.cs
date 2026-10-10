using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using FluentAssertions;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Domain.Tests.Company;

/// <summary>
/// IL-5A — Company.OpeningBalanceDate: único corte de apertura de saldos (SSOT de IL-5/6/7).
/// Configurable mientras no haya operaciones reales y coherente con las aperturas confirmadas.
/// </summary>
public sealed class CompanyOpeningBalanceDateTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Sept30 = new(2026, 9, 30);
    private static readonly DateOnly Aug31 = new(2026, 8, 31);

    private static CompanyEntity NewCompany() =>
        CompanyEntity.CreateManaged(Guid.NewGuid(), "1790016919001", "Empresa S.A.", createdBy: UserId);

    private static OpeningBalanceDateConstraints Free => new(false, []);

    private static OpeningBalanceDateConstraints With(bool operations, params DateOnly[] confirmed) =>
        new(operations, confirmed);

    [Fact]
    public void Una_empresa_nueva_no_tiene_fecha_de_apertura()
    {
        NewCompany().OpeningBalanceDate.Should().BeNull();
    }

    [Fact]
    public void Set_inicial_sin_operaciones_ni_aperturas()
    {
        var company = NewCompany();

        company.SetOpeningBalanceDate(Sept30, Free, UserId);

        company.OpeningBalanceDate.Should().Be(Sept30);
        company.UpdatedBy.Should().Be(UserId);
    }

    [Fact]
    public void Se_corrige_mientras_no_haya_operaciones_ni_aperturas_confirmadas()
    {
        var company = NewCompany();
        company.SetOpeningBalanceDate(Sept30, Free, UserId);

        company.SetOpeningBalanceDate(Aug31, Free, UserId);

        company.OpeningBalanceDate.Should().Be(Aug31);
    }

    [Fact]
    public void Repetir_la_fecha_actual_es_idempotente_aunque_este_bloqueada()
    {
        var company = NewCompany();
        company.SetOpeningBalanceDate(Sept30, Free, UserId);

        var act = () => company.SetOpeningBalanceDate(Sept30, With(true, Sept30), Guid.NewGuid());

        act.Should().NotThrow();
        company.UpdatedBy.Should().Be(UserId);
    }

    [Fact]
    public void Con_apertura_confirmada_otra_fecha_se_rechaza()
    {
        var company = NewCompany();
        company.SetOpeningBalanceDate(Sept30, Free, UserId);

        var act = () => company.SetOpeningBalanceDate(Aug31, With(false, Sept30), UserId);

        act.Should().Throw<DomainRuleViolationException>().WithMessage("*solicitada (2026-08-31)*2026-09-30*corrija o reabra*");
        company.OpeningBalanceDate.Should().Be(Sept30);
    }

    [Fact]
    public void Con_operaciones_reales_una_fecha_ya_fijada_es_definitiva()
    {
        var company = NewCompany();
        company.SetOpeningBalanceDate(Sept30, Free, UserId);

        var act = () => company.SetOpeningBalanceDate(Aug31, With(true), UserId);

        act.Should().Throw<DomainRuleViolationException>().WithMessage("*definitiva*operaciones reales*");
        company.OpeningBalanceDate.Should().Be(Sept30);
    }

    [Fact]
    public void Con_operaciones_reales_y_sin_apertura_confirmada_no_se_puede_definir()
    {
        var company = NewCompany();

        var act = () => company.SetOpeningBalanceDate(Sept30, With(true), UserId);

        act.Should().Throw<DomainRuleViolationException>().WithMessage("*operaciones reales*ninguna carga inicial*");
        company.OpeningBalanceDate.Should().BeNull();
    }

    [Fact]
    public void Compatibilidad_Sumak_acepta_la_fecha_real_de_la_apertura_existente()
    {
        // Sumak: ya opera (caja abierta) y tiene Inventario Inicial confirmado al 2026-09-30, sin fecha definida.
        var company = NewCompany();

        company.SetOpeningBalanceDate(Sept30, With(true, Sept30), UserId);

        company.OpeningBalanceDate.Should().Be(Sept30);
    }

    [Fact]
    public void Compatibilidad_Sumak_rechaza_otra_fecha()
    {
        var company = NewCompany();

        var act = () => company.SetOpeningBalanceDate(Aug31, With(true, Sept30), UserId);

        act.Should().Throw<DomainRuleViolationException>().WithMessage("*2026-09-30*");
        company.OpeningBalanceDate.Should().BeNull();
    }

    [Fact]
    public void Aperturas_confirmadas_con_fechas_distintas_bloquean_toda_definicion()
    {
        var company = NewCompany();

        var act = () => company.SetOpeningBalanceDate(Sept30, With(false, Sept30, Aug31), UserId);

        act.Should().Throw<DomainRuleViolationException>().WithMessage("*fechas distintas*");
    }

    [Fact]
    public void Motivo_de_bloqueo_refleja_el_estado()
    {
        CompanyEntity.OpeningBalanceDateLockReason(null, Free).Should().BeNull();
        CompanyEntity.OpeningBalanceDateLockReason(null, With(true, Sept30)).Should().BeNull();
        CompanyEntity.OpeningBalanceDateLockReason(Sept30, With(true)).Should().Contain("definitiva");
        CompanyEntity.OpeningBalanceDateLockReason(null, With(true)).Should().Contain("ninguna carga inicial");
    }

    // ── IL-8E: cierre definitivo de la Carga Inicial ─────────────────────────────────────────

    [Fact]
    public void Cerrar_carga_inicial_es_idempotente_e_inmoviliza_la_fecha()
    {
        var company = NewCompany();
        company.SetOpeningBalanceDate(Sept30, Free, UserId);
        company.IsInitialLoadClosed.Should().BeFalse();

        company.CloseInitialLoad(UserId);
        var closedAt = company.InitialLoadClosedAt;
        company.CloseInitialLoad(Guid.NewGuid());

        company.IsInitialLoadClosed.Should().BeTrue();
        company.InitialLoadClosedAt.Should().Be(closedAt, "un segundo cierre conserva el primero");
        company.InitialLoadClosedBy.Should().Be(UserId);
        FluentActions.Invoking(() => company.SetOpeningBalanceDate(Aug31, Free, UserId))
            .Should().Throw<DomainRuleViolationException>().WithMessage(CompanyEntity.InitialLoadClosedMessage);
        company.SetOpeningBalanceDate(Sept30, Free, UserId);
        company.OpeningBalanceDate.Should().Be(Sept30, "repetir la fecha vigente sigue siendo idempotente");
        CompanyEntity.OpeningBalanceDateLockReason(Sept30, Free, initialLoadClosed: true)
            .Should().Be(CompanyEntity.InitialLoadClosedMessage);
    }
}
