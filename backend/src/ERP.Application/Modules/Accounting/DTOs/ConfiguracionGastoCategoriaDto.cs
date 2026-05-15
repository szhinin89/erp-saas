namespace ERP.Application.Modules.Accounting.DTOs;

public sealed record ConfiguracionGastoCategoriaDto(
    Guid Id,
    string Categoria,
    Guid CuentaGastoId);
