namespace FuelIntake.Core.Model;

/// <summary>One line of the Income/Expense worksheet: a stable key (stored in intake.json) and its printed label.</summary>
public sealed record WorksheetLine(string Key, string Label);

/// <summary>The Income/Expense worksheet lines, in form order. Keys must never change once files exist.</summary>
public static class IncomeExpenseCatalog
{
    public static readonly IReadOnlyList<WorksheetLine> IncomeLines =
    [
        new("Alimony", "Alimony/Other Spousal Support"),
        new("ChildSupport", "Child Support"),
        new("Earned", "Earned Income (employment/self-employment)"),
        new("Pension", "Pension/Retirement Income from Job"),
        new("SSA", "Social Security Retirement (SSA)"),
        new("SSDI", "Social Security Disability (SSDI)"),
        new("SSI", "Supplemental Social Security (SSI)"),
        new("Unemployment", "Unemployment"),
        new("VA", "VA Disability"),
        new("WorkersComp", "Worker's Compensation"),
        new("Other", "Other"),
    ];

    public static readonly IReadOnlyList<WorksheetLine> ExpenseLines =
    [
        new("AutoIns", "Auto Insurance"),
        new("AutoReg", "Auto Registration/Repair"),
        new("AutoLoan", "Auto Loan"),
        new("Childcare", "Childcare/Adult Care"),
        new("ChildSupport", "Child Support"),
        new("Cigarettes", "Cigarettes/Vapes"),
        new("CoPay", "CO Payments (Dr/Dentist)"),
        new("CreditCard", "Credit Card"),
        new("Electricity", "Electricity"),
        new("Entertainment", "Entertainment/Subscriptions"),
        new("Groceries", "Groceries"),
        new("Gas", "Gas"),
        new("Hygiene", "Hygiene/Laundry/Paper"),
        new("HomeHeating", "Home Heating"),
        new("HomeIns", "Home Insurance"),
        new("HomeRepairs", "Home Repairs"),
        new("Medical", "Medical Insurance/Expenses"),
        new("Loans", "Loans"),
        new("Rent", "Mortgage/Rent"),
        new("Plowing", "Plowing/Water/Sewer"),
        new("Phone", "Phone/WIFI"),
        new("Pets", "Vet/Pets"),
    ];
}
