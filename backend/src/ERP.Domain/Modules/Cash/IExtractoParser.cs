namespace ERP.Domain.Modules.Cash;

public interface IStatementParser
{
    Task<IReadOnlyList<StatementParseRow>> ParseAsync(Stream stream, CancellationToken ct = default);
}
