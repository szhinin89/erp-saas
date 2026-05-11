using ERP.Domain.Common;

namespace ERP.Domain.Modules.Caja.Entities;

/// <summary>Conteo físico de caja chica y diferencia frente al saldo registrado.</summary>
public sealed class ArqueoCaja : AuditableEntity, ITenantEntity
{
    public const int ObservacionesMaxLen = 2000;

    public Guid CajaChicaId { get; private set; }
    public DateTime FechaArqueo { get; private set; }
    public decimal EfectivoFisico { get; private set; }
    public decimal Diferencia { get; private set; }
    public string Observaciones { get; private set; } = "";
    public bool Aprobado { get; private set; }

    private ArqueoCaja() { }

    public static ArqueoCaja Create(
        Guid tenantId,
        Guid cajaChicaId,
        DateTime fechaArqueo,
        decimal efectivoFisico,
        decimal saldoEsperado,
        string? observaciones,
        Guid createdBy)
    {
        if (efectivoFisico < 0)
            throw new ArgumentException("El efectivo físico no puede ser negativo.", nameof(efectivoFisico));

        var diff = efectivoFisico - saldoEsperado;
        var a = new ArqueoCaja
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            CajaChicaId     = cajaChicaId,
            FechaArqueo     = fechaArqueo,
            EfectivoFisico  = efectivoFisico,
            Diferencia      = diff,
            Observaciones   = observaciones?.Trim() ?? "",
            Aprobado        = false,
        };
        a.SetCreated(createdBy);
        return a;
    }

    public void Aprobar(Guid updatedBy)
    {
        if (Aprobado)
            throw new InvalidOperationException("El arqueo ya está aprobado.");
        Aprobado = true;
        SetUpdated(updatedBy);
    }
}
