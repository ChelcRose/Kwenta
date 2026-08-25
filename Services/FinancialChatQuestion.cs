namespace Kwenta.Services;

public enum FinancialChatQuestion
{
    TotalExpenses,
    TotalIncome,
    HighestSpendingCategory,
    LargestExpense,
    TotalBudgetRemaining,
    OverBudgetCategories,
    CategoryBudgetRemaining,
    ClosestBudgetToUsedUp,
    CurrentMonthSavings,
    PreviousMonthSavings,
    SavingsComparison,
    PreviousMonthSpending,
    SpendingComparison
}

public sealed record FinancialChatRoute(FinancialChatQuestion Question, string? CategoryName = null);

public static class FinancialChatQuestionRouter
{
    private const string CategoryBudgetPrefix = "how much do i have left for ";
    private const string CategoryBudgetSuffix = " this month";

    public static bool TryRoute(string question, out FinancialChatRoute route)
    {
        var normalized = question.Trim().TrimEnd('?', '.', '!').ToLowerInvariant();

        route = normalized switch
        {
            "how much did i spend this month" => new(FinancialChatQuestion.TotalExpenses),
            "how much income did i receive this month" => new(FinancialChatQuestion.TotalIncome),
            "where did most of my money go this month" => new(FinancialChatQuestion.HighestSpendingCategory),
            "what was my biggest expense this month" => new(FinancialChatQuestion.LargestExpense),
            "how much budget do i have left this month" => new(FinancialChatQuestion.TotalBudgetRemaining),
            "am i over budget anywhere this month" => new(FinancialChatQuestion.OverBudgetCategories),
            "which budget is closest to being used up" => new(FinancialChatQuestion.ClosestBudgetToUsedUp),
            "how much did i save this month" => new(FinancialChatQuestion.CurrentMonthSavings),
            "how much did i save last month" => new(FinancialChatQuestion.PreviousMonthSavings),
            "am i saving more than last month" => new(FinancialChatQuestion.SavingsComparison),
            "how have my savings changed from last month" => new(FinancialChatQuestion.SavingsComparison),
            "how much did i spend last month" => new(FinancialChatQuestion.PreviousMonthSpending),
            "am i spending more than last month" => new(FinancialChatQuestion.SpendingComparison),
            "how has my spending changed from last month" => new(FinancialChatQuestion.SpendingComparison),
            "compare my spending this month to last month" => new(FinancialChatQuestion.SpendingComparison),
            _ => null!
        };

        if (route is not null)
        {
            return true;
        }

        if (normalized.StartsWith(CategoryBudgetPrefix, StringComparison.Ordinal) &&
            normalized.EndsWith(CategoryBudgetSuffix, StringComparison.Ordinal))
        {
            var categoryName = normalized[
                CategoryBudgetPrefix.Length..
                ^CategoryBudgetSuffix.Length].Trim();

            if (categoryName.Length > 0)
            {
                route = new(FinancialChatQuestion.CategoryBudgetRemaining, categoryName);
                return true;
            }
        }

        route = null!;
        return false;
    }
}
