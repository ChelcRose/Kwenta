using System.Globalization;
using static Kwenta.Services.BudgetAnalysisService;

namespace Kwenta.Services;

public static class BudgetChatResponseBuilder
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public static string NoBudgets(string monthName) =>
        $"You don't have any budgets set for {monthName} yet.";

    public static string CategoryNotFound(string categoryName, string monthName) =>
        $"You don't have a current-month budget named {categoryName.Trim()} for {monthName}.";

    public static string CategoryAmbiguous(string categoryName, string monthName) =>
        $"More than one current-month budget matches {categoryName.Trim()} for {monthName}, so I can't choose one safely.";

    public static string BuildDeterministicResponse(
        FinancialChatQuestion question,
        MonthlyBudgetAnalysis analysis,
        BudgetProgressFact? selectedBudget = null) => question switch
    {
        FinancialChatQuestion.TotalBudgetRemaining =>
            $"You have {FormatPeso(analysis.TotalRemaining)} left across your {analysis.MonthName} budgets. You have spent {FormatPeso(analysis.TotalSpent)} of {FormatPeso(analysis.TotalLimit)}.",
        FinancialChatQuestion.OverBudgetCategories when analysis.OverBudgetCategories.Count == 0 =>
            $"You are not over budget in any category for {analysis.MonthName}.",
        FinancialChatQuestion.OverBudgetCategories =>
            $"You are over budget in: {string.Join(", ", analysis.OverBudgetCategories.Select(budget => $"{budget.CategoryName} ({FormatPeso(-budget.Remaining)} over)"))}.",
        FinancialChatQuestion.CategoryBudgetRemaining when selectedBudget is not null =>
            $"You have {FormatPeso(selectedBudget.Remaining)} left for {selectedBudget.CategoryName} in {analysis.MonthName}. You have spent {FormatPeso(selectedBudget.AmountSpent)} of {FormatPeso(selectedBudget.LimitAmount)}.",
        FinancialChatQuestion.ClosestBudgetToUsedUp when analysis.ClosestToUsedUp is { } closest =>
            $"{closest.CategoryName} is closest to being used up at {FormatPercentage(closest.PercentageUsed)} used, with {FormatPeso(closest.Remaining)} remaining.",
        _ => "I don't have enough budget information to answer that question."
    };

    private static string FormatPeso(decimal amount) =>
        $"₱{amount.ToString("N2", PhilippineCulture)}";

    private static string FormatPercentage(decimal percentage) =>
        $"{percentage.ToString("0.#", PhilippineCulture)}%";
}
