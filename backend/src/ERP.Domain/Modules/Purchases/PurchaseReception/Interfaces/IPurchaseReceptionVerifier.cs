using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;

/// <summary>
/// Cruza registros ya parseados contra proveedores y compras existentes del ERP.
/// Consulta repositorios de dominio — no interpreta el archivo original.
/// </summary>
public interface IPurchaseReceptionVerifier
{
    Task<IReadOnlyList<PurchaseReceptionVerifiedItem>> VerifyAsync(
        IReadOnlyList<PurchaseReceptionRecord> records,
        CancellationToken cancellationToken = default
    );
}
