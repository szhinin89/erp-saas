using FluentAssertions;
using ERP.Application.Modules.Proveedores.UseCases.CreateProveedor;
using ERP.Application.Common;
using ERP.Domain.Common.Validators;
using ERP.Domain.Proveedores.Entities;
using ERP.Domain.Proveedores.Interfaces;
using Moq;

namespace ERP.Application.Tests.Proveedores;

public sealed class CreateProveedorCommandValidatorTests
{
    private static string ValidSociedadPrivadaRuc()
    {
        for (var d = 0; d <= 9; d++)
        {
            var r = "179001691" + d + "001";
            if (RucValidator.EsRucValido(r))
                return r;
        }

        throw new InvalidOperationException("RUC de prueba no encontrado.");
    }

    private static CreateProveedorCommandValidator CreateValidator(
        Mock<IProveedorRepository>? repo = null,
        Guid? tenantId = null)
    {
        var r = repo ?? new Mock<IProveedorRepository>();
        var tid = tenantId ?? Guid.NewGuid();
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(tid);
        return new CreateProveedorCommandValidator(r.Object, tenant.Object);
    }

    [Fact]
    public async Task Ruc_duplicado_falla()
    {
        var ruc = ValidSociedadPrivadaRuc();
        var repo = new Mock<IProveedorRepository>();
        repo.Setup(x => x.ExistsRucAsync(It.IsAny<Guid>(), ruc, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var v = CreateValidator(repo);
        var result = await v.ValidateAsync(new CreateProveedorCommand(
            Proveedor.TipoJuridica,
            "ACME",
            ruc,
            "a@b.co",
            null,
            null,
            "Contado"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProveedorCommand.Ruc)
            && e.ErrorMessage.Contains("Ya existe", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012")] // 12
    [InlineData("12345678901234")] // 14
    public async Task Ruc_longitud_incorrecta_falla(string ruc)
    {
        var repo = new Mock<IProveedorRepository>();
        repo.Setup(x => x.ExistsRucAsync(It.IsAny<Guid>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var v = CreateValidator(repo);
        var result = await v.ValidateAsync(new CreateProveedorCommand(
            Proveedor.TipoJuridica,
            "ACME",
            ruc,
            null, null, null, "Contado"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProveedorCommand.Ruc));
    }

    [Fact]
    public async Task Ruc_formato_invalido_modulo_falla()
    {
        var repo = new Mock<IProveedorRepository>();
        repo.Setup(x => x.ExistsRucAsync(It.IsAny<Guid>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var v = CreateValidator(repo);
        // 13 dígitos pero dígito verificador incorrecto para sociedad privada
        var result = await v.ValidateAsync(new CreateProveedorCommand(
            Proveedor.TipoJuridica,
            "ACME",
            "1790016910001",
            null, null, null, "Contado"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProveedorCommand.Ruc)
            && e.ErrorMessage.Contains("SRI", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ruc_valido_y_no_duplicado_pasa()
    {
        var ruc = ValidSociedadPrivadaRuc();
        var repo = new Mock<IProveedorRepository>();
        repo.Setup(x => x.ExistsRucAsync(It.IsAny<Guid>(), ruc, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var v = CreateValidator(repo);
        var result = await v.ValidateAsync(new CreateProveedorCommand(
            Proveedor.TipoJuridica,
            "Proveedor OK SA",
            ruc,
            "ok@example.com",
            null,
            null,
            "Contado"));

        result.IsValid.Should().BeTrue();
    }
}
