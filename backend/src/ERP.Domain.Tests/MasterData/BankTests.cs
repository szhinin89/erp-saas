using ERP.Domain.MasterData.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.MasterData;

/// <summary>BANK-CATALOG-01: catálogo maestro de Bancos.</summary>
public sealed class BankTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_asigna_los_campos_correctamente_con_pais_por_defecto()
    {
        var bank = Bank.Create(TenantId, "pichincha", "Banco Pichincha", "Pichincha", UserId);

        bank.TenantId.Should().Be(TenantId);
        bank.CountryCode.Should().Be("EC");
        bank.Code.Should().Be("PICHINCHA");
        bank.Name.Should().Be("Banco Pichincha");
        bank.ShortName.Should().Be("Pichincha");
        bank.IsActive.Should().BeTrue();
        bank.IsSystemSeeded.Should().BeFalse();
    }

    [Fact]
    public void Create_normaliza_code_a_mayusculas_sin_espacios()
    {
        var bank = Bank.Create(TenantId, " banco pichincha ", "Banco Pichincha", null, UserId);

        bank.Code.Should().Be("BANCOPICHINCHA");
    }

    [Fact]
    public void Create_normaliza_country_code_a_mayusculas()
    {
        var bank = Bank.Create(TenantId, "GUAYAQUIL", "Banco Guayaquil", null, UserId, "ec");

        bank.CountryCode.Should().Be("EC");
    }

    [Fact]
    public void Create_permite_short_name_nulo()
    {
        var bank = Bank.Create(TenantId, "JEP", "Cooperativa JEP", null, UserId);

        bank.ShortName.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rechaza_code_vacio(string code)
    {
        var act = () => Bank.Create(TenantId, code, "Banco X", null, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rechaza_name_vacio(string name)
    {
        var act = () => Bank.Create(TenantId, "CODE", name, null, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_country_code_con_longitud_incorrecta()
    {
        var act = () => Bank.Create(TenantId, "CODE", "Banco X", null, UserId, "ECU");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_code_que_supera_el_largo_maximo()
    {
        var tooLong = new string('A', Bank.MaxCodeLength + 1);
        var act = () => Bank.Create(TenantId, tooLong, "Banco X", null, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSystemSeeded_marca_IsSystemSeeded()
    {
        var bank = Bank.CreateSystemSeeded(TenantId, "PICHINCHA", "Banco Pichincha", null, UserId);

        bank.IsSystemSeeded.Should().BeTrue();
    }

    [Fact]
    public void Update_actualiza_name_y_short_name()
    {
        var bank = Bank.Create(TenantId, "PICHINCHA", "Banco Pichincha", null, UserId);

        bank.Update("Banco Pichincha S.A.", "Pichincha", UserId);

        bank.Name.Should().Be("Banco Pichincha S.A.");
        bank.ShortName.Should().Be("Pichincha");
        bank.UpdatedAt.Should().NotBeNull();
        bank.UpdatedBy.Should().Be(UserId);
    }

    [Fact]
    public void Update_no_permite_cambiar_code_ni_country_code()
    {
        var bank = Bank.Create(TenantId, "PICHINCHA", "Banco Pichincha", null, UserId);

        bank.Update("Nuevo nombre", null, UserId);

        bank.Code.Should().Be("PICHINCHA");
        bank.CountryCode.Should().Be("EC");
    }

    [Fact]
    public void Update_rechaza_name_vacio()
    {
        var bank = Bank.Create(TenantId, "PICHINCHA", "Banco Pichincha", null, UserId);

        var act = () => bank.Update("", null, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Disable_y_Enable_alternan_IsActive_incluso_para_bancos_sembrados()
    {
        // BANK-CATALOG-01: a diferencia de otros catálogos sembrados (PaymentTerm.Disable bloquea
        // filas con IsSystemSeeded), Bank permite desactivar libremente — el ticket pide
        // activar/desactivar como único mecanismo de baja, sin excepción para los 9 bancos seed.
        var bank = Bank.CreateSystemSeeded(TenantId, "PICHINCHA", "Banco Pichincha", null, UserId);

        bank.Disable(UserId);
        bank.IsActive.Should().BeFalse();

        bank.Enable(UserId);
        bank.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Disable_ya_deshabilitado_lanza_excepcion()
    {
        var bank = Bank.Create(TenantId, "PICHINCHA", "Banco Pichincha", null, UserId);
        bank.Disable(UserId);

        var act = () => bank.Disable(UserId);

        act.Should().Throw<InvalidOperationException>();
    }
}
