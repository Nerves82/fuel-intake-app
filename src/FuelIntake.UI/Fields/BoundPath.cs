using System.Linq.Expressions;
using System.Reflection;
using FuelIntake.Core.Validation;

namespace FuelIntake.UI.Fields;

/// <summary>
/// Works out which part of the record a field edits from its @bind-Value expression, e.g.
/// <c>() =&gt; member.Ssn</c> → "household[2].ssn". Evaluates only the object chain (no compiling), so it's cheap.
/// </summary>
public static class BoundPath
{
    public static string? Of(LambdaExpression? expression, RecordPaths? paths)
    {
        if (expression is null || paths is null) return null;
        try
        {
            var body = Strip(expression.Body);
            return body switch
            {
                MemberExpression m when m.Member is PropertyInfo => paths.PathOf(Eval(m.Expression), m.Member.Name),
                MethodCallExpression { Method.Name: "get_Item", Arguments.Count: 1 } c => paths.PathOfEntry(Eval(c.Object), Eval(c.Arguments[0])),
                _ => null,
            };
        }
        catch (Exception e) when (e is InvalidOperationException or TargetException or ArgumentException or NullReferenceException)
        {
            return null;
        }
    }

    private static Expression Strip(Expression e) => e is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u ? Strip(u.Operand) : e;

    private static object? Eval(Expression? e) => Strip(e!) switch
    {
        ConstantExpression c => c.Value,
        MemberExpression { Member: FieldInfo f } m => f.GetValue(m.Expression is null ? null : Eval(m.Expression)),
        MemberExpression { Member: PropertyInfo p } m => p.GetValue(m.Expression is null ? null : Eval(m.Expression)),
        MethodCallExpression { Method.Name: "get_Item" } c => c.Method.Invoke(Eval(c.Object), c.Arguments.Select(Eval).ToArray()),
        var other => Expression.Lambda(other).Compile().DynamicInvoke(),
    };
}
