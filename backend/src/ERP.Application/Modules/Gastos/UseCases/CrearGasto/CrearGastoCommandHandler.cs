using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Gastos.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Gastos.Entities;
using ERP.Domain.Gastos.Interfaces;
using ERP.Domain.Proveedores.Entities;
using ERP.Domain.Proveedores.Interfaces;

namespace ERP.Application.Modules.Gastos.UseCases.CrearGasto;

public sealed class CrearGastoCommandHandler
    : IRequestHandler<CrearGastoCommand, Result<GastoFacturaDto>>
{
    private readonly IGastoFacturaRepository   _gastos;
    private readonly IProveedorRepository    _proveedorRepo;
    private readonly IXmlFacturaParser       _parser;
    private readonly IFileStorage            _storage;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant          _tenant;
    private readonly ICurrentUser            _user;
    private readonly ILogger<CrearGastoCommandHandler> _logger;

    public CrearGastoCommandHandler(
        IGastoFacturaRepository gastos,
        IProveedorRepository proveedorRepo,
        IXmlFacturaParser parser,
        IFileStorage storage,
        IUserActivityRepository activity,
        ICurrentTenant tenant,
        ICurrentUser user,
        ILogger<CrearGastoCommandHandler> logger)
    {
        _gastos        = gastos;
        _proveedorRepo = proveedorRepo;
        _parser        = parser;
        _storage       = storage;
        _activity      = activity;
        _tenant        = tenant;
        _user          = user;
        _logger        = logger;
    }

    public Task<Result<GastoFacturaDto>> Handle(CrearGastoCommand command, CancellationToken ct)
        => command.Modo == ModoCreacionGasto.Xml
            ? HandleXml(command, ct)
            : HandleManual(command, ct);

    private async Task<Result<GastoFacturaDto>> HandleXml(CrearGastoCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        FacturaParseResult parsed;
        try
        {
            using var stream = new MemoryStream(command.XmlContent!);
            parsed = await _parser.ParseAsync(stream, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Fallo al parsear XML de gasto (tenant {TenantId}, usuario {UserId}).",
                tenantId, userId);
            return Result<GastoFacturaDto>.Failure($"Error al leer el XML: {ex.Message}");
        }

        if (await _gastos.ExistsClaveAccesoAsync(tenantId, parsed.ClaveAcceso, ct))
            return Result<GastoFacturaDto>.Failure(
                $"Ya existe un gasto registrado con la clave de acceso '{parsed.ClaveAcceso}'.");

        var proveedor = await ObtenerOCrearProveedor(
            tenantId, parsed.RucProveedor, parsed.RazonSocialProveedor, userId, ct);

        var xmlPath = $"facturas/gastos/{parsed.ClaveAcceso}.xml";
        using var xmlStream = new MemoryStream(command.XmlContent!);
        await _storage.SaveAsync(xmlPath, xmlStream, ct);

        var concepto = parsed.Items.Count > 0
            ? parsed.Items[0].Descripcion.Trim()
            : $"Gasto {parsed.NumeroFactura}";

        GastoFactura gasto;
        try
        {
            gasto = GastoFactura.CreateFromXml(
                tenantId,
                proveedor.Id,
                parsed.ClaveAcceso,
                parsed.NumeroFactura,
                parsed.FechaEmision,
                concepto,
                command.CategoriaGasto!.Trim(),
                parsed.Subtotal,
                parsed.Impuesto,
                parsed.Total,
                xmlPath,
                command.Observaciones,
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Fallo al construir gasto desde XML parseado (tenant {TenantId}, clave {Clave}).",
                tenantId, parsed.ClaveAcceso);
            return Result<GastoFacturaDto>.Failure(ex.Message);
        }

        await _gastos.AddAsync(gasto, ct);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "gastos", action: "gasto.crear.xml",
            entityType: "GastoFactura", entityId: gasto.Id,
            description: $"{parsed.NumeroFactura} — {proveedor.RazonSocial}"), ct);

        await _gastos.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Gasto creado desde XML: id {GastoId}, tenant {TenantId}, clave {ClaveAcceso}.",
            gasto.Id, tenantId, parsed.ClaveAcceso);

        return Result<GastoFacturaDto>.Success(ToDto(gasto));
    }

    private async Task<Result<GastoFacturaDto>> HandleManual(CrearGastoCommand command, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        if (command.Total!.Value > GastoFactura.TotalRequiereXml)
            return Result<GastoFacturaDto>.Failure(
                $"Los gastos con total estrictamente mayor a {GastoFactura.TotalRequiereXml} deben registrarse con comprobante XML.");

        Guid? proveedorId = command.ProveedorId;
        if (proveedorId.HasValue)
        {
            var p = await _proveedorRepo.GetByIdAsync(tenantId, proveedorId.Value, ct);
            if (p is null)
                return Result<GastoFacturaDto>.Failure("Proveedor no encontrado en el tenant.");
            if (!p.IsActive)
                return Result<GastoFacturaDto>.Failure("El proveedor está deshabilitado.");
        }

        GastoFactura gasto;
        try
        {
            gasto = GastoFactura.CreateManual(
                tenantId,
                proveedorId,
                command.FechaEmision!.Value,
                command.Concepto!,
                command.CategoriaGasto!,
                command.Subtotal!.Value,
                command.Impuesto!.Value,
                command.Total!.Value,
                command.Observaciones,
                userId);
        }
        catch (Exception ex)
        {
            return Result<GastoFacturaDto>.Failure(ex.Message);
        }

        await _gastos.AddAsync(gasto, ct);

        await _activity.AddAsync(UserActivity.Create(
            tenantId, userId, _user.Email, _user.FullName,
            module: "gastos", action: "gasto.crear.manual",
            entityType: "GastoFactura", entityId: gasto.Id,
            description: gasto.Concepto), ct);

        await _gastos.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Gasto creado manual: id {GastoId}, tenant {TenantId}, concepto {Concepto}.",
            gasto.Id, tenantId, gasto.Concepto);

        return Result<GastoFacturaDto>.Success(ToDto(gasto));
    }

    private async Task<Proveedor> ObtenerOCrearProveedor(
        Guid tenantId, string ruc, string razonSocial, Guid userId, CancellationToken ct)
    {
        var existentes = await _proveedorRepo.GetAsync(
            tenantId, activeFilter: null, search: ruc, tipoPersona: null, ct);

        var existente = existentes.FirstOrDefault(p => p.Ruc == ruc);
        if (existente is not null) return existente;

        var tipo = ruc[2] - '0' is >= 0 and <= 5 ? Proveedor.TipoNatural : Proveedor.TipoJuridica;
        var nuevo = Proveedor.Create(
            tenantId, tipo, razonSocial, ruc,
            correo: null, telefono: null, direccion: null,
            condicionPago: "Contado", userId);

        await _proveedorRepo.AddAsync(nuevo, ct);
        return nuevo;
    }

    private static GastoFacturaDto ToDto(GastoFactura g) => new(
        g.Id,
        g.ClaveAcceso,
        g.FechaEmision,
        g.ProveedorId,
        g.NumeroFactura,
        g.Concepto,
        g.CategoriaGasto,
        g.Subtotal,
        g.Impuesto,
        g.Total,
        g.Estado,
        g.XmlPath,
        g.Observaciones,
        g.AsientoContableId,
        g.CreatedAt);
}
