using System.Linq.Expressions;
using FuelIntake.Core.Validation;
using FuelIntake.UI.Services;
using Microsoft.AspNetCore.Components;

namespace FuelIntake.UI.Fields;

/// <summary>
/// Base for all input fields: a label, an optional hint, a value with two-way binding,
/// and a call to <see cref="IntakeSession.Changed"/> after every edit (recalculate + autosave).
/// </summary>
public abstract class FieldBase<T> : ComponentBase
{
    private static int counter;

    [Inject] protected IntakeSession Session { get; set; } = default!;

    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter] public string? Hint { get; set; }
    [Parameter] public T Value { get; set; } = default!;
    [Parameter] public EventCallback<T> ValueChanged { get; set; }
    /// <summary>Spans the full row (long text).</summary>
    [Parameter] public bool Wide { get; set; }

    protected string Id { get; } = "f" + Interlocked.Increment(ref counter);

    /// <summary>Filled in by @bind-Value: tells the field which part of the record it edits.</summary>
    [Parameter] public Expression<Func<T>>? ValueExpression { get; set; }

    /// <summary>The field's record path when it can't be worked out from @bind-Value ("incomeExpenses.income.SSA").</summary>
    [Parameter] public string? Path { get; set; }

    /// <summary>The most serious validation issue about this field (shown on the field), or null.</summary>
    protected ValidationIssue? Issue => Session.IssueAt(FieldPath);

    /// <summary>The record path this field edits ("household[2].ssn"), or null when it can't be told.</summary>
    protected string? FieldPath => Path ?? BoundPath.Of(ValueExpression, Session.Paths);

    protected async Task SetAsync(T value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        Session.Changed();
    }

    /// <summary>Message shown under the field when the entry is wrong, or null.</summary>
    protected virtual string? Problem => null;
}
