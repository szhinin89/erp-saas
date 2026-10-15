using System.Globalization;
using System.Linq.Expressions;

namespace ERP.Domain.Configuration.Definitions.Modules;

public static class ElectronicDocumentRetryMaximum
{
    public const int Default = 5;
    public const int Minimum = 1;
    public const int Maximum = 10;
    public static Expression<Func<string?, int>> EffectiveValue { get; } = Build(Default);
    private static readonly Func<string?, int> ResolveValue = EffectiveValue.Compile();
    private static readonly Func<string?, int> ValidateValue = Build(0).Compile();
    public static bool IsValid(string? raw) => ValidateValue(raw) >= Minimum;
    public static int Resolve(string? raw) => ResolveValue(raw);

    private static Expression<Func<string?, int>> Build(int fallback)
    {
        var raw = Expression.Parameter(typeof(string), "raw");
        var trimmed = Expression.Call(raw, typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!);
        Expression result = Expression.Constant(fallback);
        for (var value = Maximum; value >= Minimum; value--)
        {
            var pattern = @"^\+?0*" + value.ToString(CultureInfo.InvariantCulture) + "$";
            var valid = Expression.AndAlso(Expression.NotEqual(raw, Expression.Constant(null, typeof(string))),
                Expression.Call(typeof(System.Text.RegularExpressions.Regex), nameof(System.Text.RegularExpressions.Regex.IsMatch),
                    Type.EmptyTypes, trimmed, Expression.Constant(pattern)));
            result = Expression.Condition(valid, Expression.Constant(value), result);
        }
        return Expression.Lambda<Func<string?, int>>(result, raw);
    }
}
