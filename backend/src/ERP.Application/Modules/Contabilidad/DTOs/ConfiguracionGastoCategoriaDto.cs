namespace ERP.Application.Modules.Contabilidad.DTOs;

public sealed record ConfiguracionGastoCategoriaDto(
    Guid Id,
    string Categoria,
    Guid CuentaGastoId);
