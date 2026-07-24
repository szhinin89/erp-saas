using ERP.Application.Access.UseCases.AssignTemporaryPasswordAdmin;
using FluentAssertions;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Reglas: Username obligatorio, TemporaryPassword obligatorio + ApplyPasswordComplexity() —
/// misma política que CreateSystemUserCommandValidator/ChangeMyPasswordCommandValidator
/// (PasswordComplexityRules: mínimo 8 caracteres, una mayúscula, un número).
/// </summary>
public sealed class AssignTemporaryPasswordAdminCommandValidatorTests
{
    private readonly AssignTemporaryPasswordAdminCommandValidator _validator = new();

    [Fact]
    public void Command_valido_no_produce_errores()
    {
        var result = _validator.Validate(new AssignTemporaryPasswordAdminCommand("ana.perez", "Temp0ral!"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Username_vacio_produce_error(string username)
    {
        var result = _validator.Validate(new AssignTemporaryPasswordAdminCommand(username, "Temp0ral!"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AssignTemporaryPasswordAdminCommand.Username));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TemporaryPassword_vacio_produce_error(string password)
    {
        var result = _validator.Validate(new AssignTemporaryPasswordAdminCommand("ana.perez", password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AssignTemporaryPasswordAdminCommand.TemporaryPassword));
    }

    [Fact]
    public void TemporaryPassword_menor_a_8_caracteres_produce_error()
    {
        var result = _validator.Validate(new AssignTemporaryPasswordAdminCommand("ana.perez", "Ab1"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AssignTemporaryPasswordAdminCommand.TemporaryPassword)
            && e.ErrorMessage == "La contraseña debe tener al menos 8 caracteres.");
    }

    [Fact]
    public void TemporaryPassword_sin_mayuscula_produce_error()
    {
        var result = _validator.Validate(new AssignTemporaryPasswordAdminCommand("ana.perez", "temp0ral!"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AssignTemporaryPasswordAdminCommand.TemporaryPassword)
            && e.ErrorMessage == "La contraseña debe tener al menos una mayúscula.");
    }

    [Fact]
    public void TemporaryPassword_sin_numero_produce_error()
    {
        var result = _validator.Validate(new AssignTemporaryPasswordAdminCommand("ana.perez", "Temporal!"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AssignTemporaryPasswordAdminCommand.TemporaryPassword)
            && e.ErrorMessage == "La contraseña debe tener al menos un número.");
    }

    [Fact]
    public void TemporaryPassword_cumple_las_tres_reglas_no_produce_error_de_complejidad()
    {
        var result = _validator.Validate(new AssignTemporaryPasswordAdminCommand("ana.perez", "Temp0ral!"));

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(AssignTemporaryPasswordAdminCommand.TemporaryPassword));
    }
}
