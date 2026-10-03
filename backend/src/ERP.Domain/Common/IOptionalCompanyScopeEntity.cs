namespace ERP.Domain.Common;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 (ADR-039 D3) — entidad cuyo alcance es de EMPRESA
/// (<see cref="TenantId"/> y <see cref="CompanyId"/> presentes) o de INSTANCIA (ambos null).
/// <para>
/// Filtro global (EnterpriseQueryFilterConfigurator): igual que <see cref="ICompanyOperationalEntity"/>
/// — tenant y empresa del contexto, fail-closed —; una fila de instancia (null) nunca coincide con
/// ningún contexto de empresa (comparación SQL con NULL = falso), así que solo es visible por el
/// patrón explícito de plataforma. "No aplica" es NULL: nunca un centinela como <c>Guid.Empty</c>,
/// que en el ERP ya significa "sin contexto" / "tenant global".
/// </para>
/// Uso restringido (allowlist en ERP.Architecture.Tests): solo Communications.
/// </summary>
public interface IOptionalCompanyScopeEntity
{
    Guid? TenantId { get; }
    Guid? CompanyId { get; }
}
