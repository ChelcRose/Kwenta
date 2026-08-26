using System.Text.RegularExpressions;

namespace Kwenta.Services;

public enum FinancialChatQuestion
{
    TotalExpenses,
    TotalIncome,
    HighestSpendingCategory,
    TopSpendingCategory,
    LargestExpense,
    UnusuallyLargeExpense,
    TotalBudgetRemaining,
    OverBudgetCategories,
    CategoryBudgetRemaining,
    ClosestBudgetToUsedUp,
    BudgetOverspendingProjection,
    CurrentMonthSavings,
    PreviousMonthSavings,
    SavingsComparison,
    PreviousMonthSpending,
    SpendingComparison,
    AllowanceRemaining,
    AllowanceAllocations,
    PurchaseAffordability,
    PurchasePlan
}

public sealed record FinancialChatRoute(
    FinancialChatQuestion Question,
    string? CategoryName = null,
    PurchaseRequest? Purchase = null);

public static class FinancialChatQuestionRouter
{
    private const string CategoryBudgetPrefix = "how much do i have left for ";
    private const string CategoryBudgetSuffix = " this month";

    public static bool TryRoute(string question, out FinancialChatRoute route)
    {
        if (PurchaseQuestionParser.TryParsePlan(question, out var purchasePlan))
        {
            route = new(FinancialChatQuestion.PurchasePlan, Purchase: purchasePlan);
            return true;
        }

        if (PurchaseQuestionParser.TryParse(question, out var purchase))
        {
            route = new(FinancialChatQuestion.PurchaseAffordability, Purchase: purchase);
            return true;
        }

        var normalized = NormalizeQuestion(question);

        if (IsAllowanceAllocationQuestion(normalized))
        {
            route = new(FinancialChatQuestion.AllowanceAllocations);
            return true;
        }

        if (IsAllowanceRemainingQuestion(normalized))
        {
            route = new(FinancialChatQuestion.AllowanceRemaining);
            return true;
        }

        route = normalized switch
        {
            "how much did i spend this month" => new(FinancialChatQuestion.TotalExpenses),
            "how much income did i receive this month" => new(FinancialChatQuestion.TotalIncome),
            "where did most of my money go this month" => new(FinancialChatQuestion.HighestSpendingCategory),
            "what is my top spending category this month" => new(FinancialChatQuestion.TopSpendingCategory),
            "which category did i spend the most on this month" => new(FinancialChatQuestion.TopSpendingCategory),
            "what category did i spend the most on this month" => new(FinancialChatQuestion.TopSpendingCategory),
            "what was my biggest expense this month" => new(FinancialChatQuestion.LargestExpense),
            "did i have any unusually large expenses this month" => new(FinancialChatQuestion.UnusuallyLargeExpense),
            "what was my unusually large transaction this month" => new(FinancialChatQuestion.UnusuallyLargeExpense),
            "did i make any big purchases this month" => new(FinancialChatQuestion.UnusuallyLargeExpense),
            "how much budget do i have left this month" => new(FinancialChatQuestion.TotalBudgetRemaining),
            "am i over budget anywhere this month" => new(FinancialChatQuestion.OverBudgetCategories),
            "which budget is closest to being used up" => new(FinancialChatQuestion.ClosestBudgetToUsedUp),
            "am i likely to go over budget this month" => new(FinancialChatQuestion.BudgetOverspendingProjection),
            "which budgets am i projected to exceed" => new(FinancialChatQuestion.BudgetOverspendingProjection),
            "am i on track with my budgets this month" => new(FinancialChatQuestion.BudgetOverspendingProjection),
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

    private static bool IsAllowanceRemainingQuestion(string normalized)
    {
        if (normalized is
            "how much do i have left" or
            "how much money do i have left" or
            "how much have i got left" or
            "how much is left" or
            "what do i have left" or
            "what is left" or
            "what's left")
        {
            return true;
        }

        return Regex.IsMatch(normalized, @"\ballowance\b", RegexOptions.CultureInvariant) &&
               Regex.IsMatch(
                   normalized,
                   @"\b(?:left|remaining|remain|balance|available|overview|status|progress|spent|spend|doing)\b",
                   RegexOptions.CultureInvariant);
    }

    private static bool IsAllowanceAllocationQuestion(string normalized) =>
        Regex.IsMatch(normalized, @"\ballowance\b", RegexOptions.CultureInvariant) &&
        Regex.IsMatch(
            normalized,
            @"\b(?:allocate|allocated|allocation|allocations|distribute|distributed|distribution|split|accounts?|where|breakdown)\b",
            RegexOptions.CultureInvariant);

    private static string NormalizeQuestion(string question)
    {
        var withoutTrailingPunctuation = Regex.Replace(
            question.Trim(),
            @"\s*[?.!]+\s*$",
            string.Empty,
            RegexOptions.CultureInvariant);

        return Regex.Replace(
                withoutTrailingPunctuation,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim()
            .ToLowerInvariant();
    }
}
