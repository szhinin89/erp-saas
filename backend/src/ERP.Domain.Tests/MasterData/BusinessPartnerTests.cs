using ERP.Domain.MasterData.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.MasterData;

/// <summary>
/// Verifica que BusinessPartner nunca pueda quedar con una combinación inconsistente entre
/// TaxIdentification y LegalEntityTypeCode, en Create/UpdateProfile/UpdateIdentification.
/// </summary>
public sealed class BusinessPartnerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // RUC Persona Natural válido (3.er dígito 0-5)
    private const string RucPersonaNatural = "0302126842001";

    // RUC Sociedad Privada válido (3.er dígito 9)
    private const string RucSociedadPrivada = "1791352688001";

    // CI válida
    private const string CiValida = "0302126842";

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public void Create_con_RUC_sin_LegalEntityTypeCode_infiere_automaticamente()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "04",
            RucSociedadPrivada,
            legalEntityTypeCode: null,
            legalName: "Empresa Prueba",
            createdBy: UserId
        );

        bp.LegalEntityTypeCode.Should().Be(2);
    }

    [Fact]
    public void Create_con_CI_sin_LegalEntityTypeCode_infiere_PersonaNatural()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "05",
            CiValida,
            legalEntityTypeCode: null,
            legalName: "Juan Perez",
            createdBy: UserId
        );

        bp.LegalEntityTypeCode.Should().Be(1);
    }

    [Fact]
    public void Create_con_RUC_y_LegalEntityTypeCode_coincidente_es_aceptado()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "04",
            RucPersonaNatural,
            legalEntityTypeCode: 1,
            legalName: "Sebastian Zhinin",
            createdBy: UserId
        );

        bp.LegalEntityTypeCode.Should().Be(1);
    }

    [Fact]
    public void Create_con_RUC_y_LegalEntityTypeCode_contradictorio_lanza_excepcion()
    {
        var act = () =>
            BusinessPartner.Create(
                TenantId,
                "04",
                RucPersonaNatural, // infiere 1
                legalEntityTypeCode: 2, // contradice
                legalName: "Empresa Prueba",
                createdBy: UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_Pasaporte_sin_LegalEntityTypeCode_lanza_excepcion()
    {
        // No puede inferirse — el dato es obligatorio, nunca se asume Persona Natural.
        var act = () =>
            BusinessPartner.Create(
                TenantId,
                "06",
                "P12345678",
                legalEntityTypeCode: null,
                legalName: "Extranjero SA",
                createdBy: UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_RUC_institucion_publica_y_LegalEntityTypeCode_natural_lanza_excepcion()
    {
        // RUC público (3.er dígito 6) infiere Institución Pública (3); código explícito 1 contradice.
        var act = () =>
            BusinessPartner.Create(
                TenantId,
                "04",
                "1760000070001",
                legalEntityTypeCode: 1,
                legalName: "Entidad Pública",
                createdBy: UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_Pasaporte_y_LegalEntityTypeCode_explicito_lo_usa()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "06",
            "P12345678",
            legalEntityTypeCode: 2,
            legalName: "Extranjero SA",
            createdBy: UserId
        );

        bp.LegalEntityTypeCode.Should().Be(2);
    }

    // ── UpdateProfile ────────────────────────────────────────────────────────

    [Fact]
    public void UpdateProfile_con_identificacion_inferible_rechaza_valor_contradictorio()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "04",
            RucPersonaNatural,
            legalEntityTypeCode: null,
            legalName: "Sebastian Zhinin",
            createdBy: UserId
        );

        var act = () => bp.UpdateProfile("Sebastian Zhinin", legalEntityTypeCode: 2, UserId);

        act.Should().Throw<ArgumentException>();
        bp.LegalEntityTypeCode.Should().Be(1, "no debe mutar ante un intento inválido");
    }

    [Fact]
    public void UpdateProfile_con_identificacion_inferible_acepta_valor_coincidente()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "04",
            RucPersonaNatural,
            legalEntityTypeCode: null,
            legalName: "Sebastian Zhinin",
            createdBy: UserId
        );

        bp.UpdateProfile("Sebastian Zhinin Actualizado", legalEntityTypeCode: 1, UserId);

        bp.LegalEntityTypeCode.Should().Be(1);
    }

    [Fact]
    public void UpdateProfile_con_identificacion_no_inferible_permite_cambiar_LegalEntityType()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "06",
            "P12345678",
            legalEntityTypeCode: 1,
            legalName: "Extranjero SA",
            createdBy: UserId
        );

        bp.UpdateProfile("Extranjero SA", legalEntityTypeCode: 2, UserId);

        bp.LegalEntityTypeCode.Should().Be(2);
    }

    [Fact]
    public void UpdateProfile_con_identificacion_no_inferible_sin_valor_lanza_excepcion()
    {
        // Mismo requisito que en Create: si no puede inferirse, el dato sigue siendo obligatorio.
        var bp = BusinessPartner.Create(
            TenantId,
            "06",
            "P12345678",
            legalEntityTypeCode: 1,
            legalName: "Extranjero SA",
            createdBy: UserId
        );

        var act = () => bp.UpdateProfile("Extranjero SA", legalEntityTypeCode: null, UserId);

        act.Should().Throw<ArgumentException>();
    }

    // ── UpdateIdentification ─────────────────────────────────────────────────

    [Fact]
    public void UpdateIdentification_a_identificacion_inferible_recalcula_LegalEntityTypeCode()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "06",
            "P12345678",
            legalEntityTypeCode: 1, // valor manual inicial, sin relación con RUC
            legalName: "Persona X",
            createdBy: UserId
        );

        bp.UpdateIdentification("04", RucSociedadPrivada, UserId);

        bp.LegalEntityTypeCode.Should().Be(2, "la nueva identificación es la fuente de verdad");
    }

    [Fact]
    public void UpdateIdentification_a_identificacion_no_inferible_conserva_LegalEntityTypeCode()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "04",
            RucSociedadPrivada,
            legalEntityTypeCode: null,
            legalName: "Empresa Prueba",
            createdBy: UserId
        );

        bp.UpdateIdentification("06", "P12345678", UserId);

        bp.LegalEntityTypeCode.Should()
            .Be(2, "sin inferencia posible se conserva el valor existente, nunca se resetea");
    }

    [Fact]
    public void UpdateIdentification_nunca_deja_estado_inconsistente()
    {
        var bp = BusinessPartner.Create(
            TenantId,
            "05",
            CiValida,
            legalEntityTypeCode: null,
            legalName: "Juan Perez",
            createdBy: UserId
        );

        bp.UpdateIdentification("04", RucSociedadPrivada, UserId);

        var inferredFromCurrentIdentification = bp.Identification.TryInferLegalEntityTypeCode();
        inferredFromCurrentIdentification.Should().Be(bp.LegalEntityTypeCode);
    }
}
