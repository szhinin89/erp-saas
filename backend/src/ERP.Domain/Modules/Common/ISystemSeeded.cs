namespace ERP.Domain.Common;

/// <summary>
/// Marca una entidad capaz de distinguir un registro sembrado automáticamente por el Bootstrap del
/// sistema (Tipo A: infraestructura obligatoria — p. ej. Sucursal Principal, Caja Principal, Consumidor
/// Final — o Tipo C: catálogo auxiliar protegido — p. ej. Marca "No aplica") de un registro creado
/// normalmente por un usuario. Única fuente de verdad de la distinción — ver política de Bootstrap en
/// <c>CLAUDE.md</c>.
///
/// <see cref="IsSystemSeeded"/> no bloquea nada por sí solo: cada entidad decide, método por método,
/// qué mutaciones invocan <see cref="SystemSeedGuard.EnsureEditable"/> sobre este flag. Una
/// <c>DocumentSequence</c> puede incrementar su numeración libremente; un <c>PriceList</c> sembrado no
/// debería poder deshabilitarse porque es el único con <c>IsDefault=true</c>. La protección es siempre
/// específica de la operación, nunca un bloqueo total del registro.
/// </summary>
public interface ISystemSeeded
{
    bool IsSystemSeeded { get; }
}
