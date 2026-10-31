using System.Linq.Expressions;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Política de reintento automático (job Hangfire). SSOT de estados/backoff;
/// el máximo procede de la configuración efectiva de empresa. La elegibilidad existe una sola vez, como predicado
/// traducible por EF (<see cref="AutomaticRetryCandidate"/>): la consulta de candidatos filtra con
/// él antes del lote y el job no vuelve a decidir.
/// </summary>
public static class ElectronicDocumentRetryPolicy
{
    /// <summary>Backoff tras n intentos: <c>min(Base · 2^n, Max)</c> → 1/2/4/8/16 minutos.</summary>
    public const double BaseBackoffMinutes = 1;

    public const double MaxBackoffMinutes = 16;

    /// <summary>
    /// R11 (auditoría SRI 2.34) — estados con resultado SRI pendiente que el job atiende (Caso #32: solo
    /// consulta). "El tiempo estimado de entrega de la autorización o motivos de errores de un
    /// comprobante, será de un tiempo máximo de 24 horas a partir de la respuesta de RECIBIDA" (Ficha
    /// Técnica Offline §7.5; nota 2 de la tabla de errores): dentro de <see cref="SriProcessingWindow"/>
    /// estos estados siguen consultándose aunque superen el máximo de la empresa, con backoff acotado
    /// a <see cref="SriOutcomePendingMaxBackoffMinutes"/>. Dispatching sigue siendo solo manual (ADR-036).
    /// </summary>
    public static readonly IReadOnlyList<ElectronicDocumentState> SriOutcomePendingStates =
    [
        ElectronicDocumentState.Signed,
        ElectronicDocumentState.Received,
    ];

    /// <summary>Tope del backoff de las consultas de resultado pendiente: 1/2/4/8/16/32/60/60… minutos.</summary>
    public const double SriOutcomePendingMaxBackoffMinutes = 60;

    /// <summary>
    /// Plazo máximo de procesamiento del SRI, contado desde RECIBIDA (primera transición a Received del
    /// timeline). Sin evidencia de recepción no hay plazo calculable: la consulta sigue (fail-closed).
    /// </summary>
    public static readonly TimeSpan SriProcessingWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Estados que el job automático de reintentos recoge como candidatos. Fuente única para
    /// <c>GetRetryCandidatesAsync</c> y para el contador "Reintentos pendientes" del Monitor.
    /// Excluye deliberadamente Dispatching (solo consulta manual, ADR-036), DeadLetter (requiere
    /// acción manual) y todo estado terminal. No define qué acepta el reintento manual
    /// (<c>IElectronicDocumentIssuer.RetryAsync</c>), que conserva sus propios guards.
    /// </summary>
    public static readonly IReadOnlyList<ElectronicDocumentState> AutomaticRetryStates =
    [
        ElectronicDocumentState.Signed,
        ElectronicDocumentState.Received,
        ElectronicDocumentState.Draft,
        ElectronicDocumentState.Failed,
    ];

    /// <summary>
    /// Documento elegible para reintento automático en <paramref name="nowUtc"/>: estado atendido por
    /// el job, menos de <paramref name="maxAttempts"/> intentos (o resultado SRI pendiente, ver
    /// <see cref="SriOutcomePendingStates"/>: el fin de la ventana lo decide el issuer al pasar a
    /// DeadLetter) y backoff vencido (sin intento previo, siempre elegible). La preferencia por empresa
    /// no forma parte de la política.
    /// </summary>
    public static Expression<Func<ElectronicDocument, bool>> AutomaticRetryCandidate(DateTime nowUtc, int maxAttempts = ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default) =>
        x => AutomaticRetryStates.Contains(x.CurrentState)
            && (x.RetryCount < maxAttempts || SriOutcomePendingStates.Contains(x.CurrentState))
            && (
                x.LastAttemptUtc == null
                || x.LastAttemptUtc.Value.AddMinutes(
                    Math.Min(
                        BaseBackoffMinutes * Math.Pow(2, x.RetryCount),
                        SriOutcomePendingStates.Contains(x.CurrentState)
                            ? SriOutcomePendingMaxBackoffMinutes
                            : MaxBackoffMinutes
                    )
                ) <= nowUtc
            );
    public static Expression<Func<ElectronicDocument, bool>> AutomaticRetryCandidate(
        DateTime nowUtc, Expression<Func<ElectronicDocument, string?>> setting)
    {
        var candidate = AutomaticRetryCandidate(nowUtc);
        var maximum = WithinMaximum(setting);
        var limit = new Substitute(maximum.Parameters[0], candidate.Parameters[0]).Visit(maximum.Body)!;
        // Replace only the maximum comparison; states/backoff remain the same tree.
        var body = new MaximumSubstitute(limit).Visit(candidate.Body)!;
        return Expression.Lambda<Func<ElectronicDocument, bool>>(body, candidate.Parameters);
    }
    private sealed class MaximumSubstitute(Expression limit) : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node) =>
            node.NodeType == ExpressionType.LessThan && node.Left is MemberExpression { Member.Name: nameof(ElectronicDocument.RetryCount) }
                ? limit : base.VisitBinary(node);
    }
    public static Expression<Func<ElectronicDocument, bool>> WithinMaximum(
        Expression<Func<ElectronicDocument, string?>> setting)
    {
        var effective = ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.EffectiveValue;
        var maximum = new Substitute(effective.Parameters[0], setting.Body).Visit(effective.Body)!;
        return Expression.Lambda<Func<ElectronicDocument, bool>>(
            Expression.LessThan(Expression.Property(setting.Parameters[0], nameof(ElectronicDocument.RetryCount)), maximum),
            setting.Parameters);
    }
    /// <summary>
    /// Mismo presupuesto que <see cref="AutomaticRetryCandidate(DateTime, Expression{Func{ElectronicDocument, string?}})"/>
    /// sin estados ni backoff (contador "Reintentos pendientes" del Monitor): dentro del máximo
    /// efectivo, o con resultado SRI pendiente (R11).
    /// </summary>
    public static Expression<Func<ElectronicDocument, bool>> WithinAutomaticBudget(
        Expression<Func<ElectronicDocument, string?>> setting)
    {
        var maximum = WithinMaximum(setting);
        Expression<Func<ElectronicDocument, bool>> pending = x => SriOutcomePendingStates.Contains(x.CurrentState);
        var pendingBody = new Substitute(pending.Parameters[0], maximum.Parameters[0]).Visit(pending.Body)!;
        return Expression.Lambda<Func<ElectronicDocument, bool>>(
            Expression.OrElse(maximum.Body, pendingBody), maximum.Parameters);
    }
    private sealed class Substitute(Expression from, Expression to) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node) => node == from ? to : base.Visit(node);
    }
}
