using System.Globalization;
using static Kwenta.Services.TransactionAnalysisService;

namespace Kwenta.Services;

public static class FinancialChatResponseBuilder
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public static string BuildDeterministicResponse(
        FinancialChatQuestion question,
        MonthlyTransactionAnalysis analysis) => question switch
    {
        FinancialChatQuestion.TotalExpenses =>
            $"You spent {FormatPeso(analysis.TotalExpenses)} in {analysis.MonthName}.",
        FinancialChatQuestion.TotalIncome =>
            $"You received {FormatPeso(analysis.TotalIncome)} in income in {analysis.MonthName}.",
        FinancialChatQuestion.HighestSpendingCategory when analysis.HighestSpendingCategory is { } category =>
            $"Most of your expense spending in {analysis.MonthName} went to {category.CategoryName}, totaling {FormatPeso(category.Amount)}.",
        FinancialChatQuestion.HighestSpendingCategory =>
            $"You have no expenses recorded for {analysis.MonthName}.",
        FinancialChatQuestion.TopSpendingCategory when
            analysis.HighestSpendingCategory is { } topCategory &&
            analysis.HighestSpendingCategoryPercentage is { } percentage =>
            $"Your top spending category in {analysis.MonthName} is {topCategory.CategoryName}: {FormatPeso(topCategory.Amount)} of {FormatPeso(analysis.TotalExpenses)} total spending ({FormatPercentage(percentage)}).",
        FinancialChatQuestion.TopSpendingCategory =>
            $"You don't have any expenses recorded for {analysis.MonthName} yet.",
        FinancialChatQuestion.LargestExpense when analysis.LargestExpense is { } expense =>
            $"Your biggest expense in {analysis.MonthName} was {FormatPeso(expense.Amount)} at {expense.Merchant} in {expense.CategoryName}.",
        FinancialChatQuestion.LargestExpense =>
            $"You have no expenses recorded for {analysis.MonthName}.",
        FinancialChatQuestion.UnusuallyLargeExpense when !analysis.HasEnoughLargeTransactionHistory =>
            $"You need at least 3 expenses in {analysis.MonthName} before Kwenta can identify an unusually large transaction. You currently have {analysis.ExpenseCount}.",
        FinancialChatQuestion.UnusuallyLargeExpense when analysis.UnusuallyLargeExpense is { } largeExpense =>
            BuildLargeExpenseResponse(analysis, largeExpense),
        FinancialChatQuestion.UnusuallyLargeExpense =>
            $"No unusually large expense stands out in {analysis.MonthName}. Your average expense was {FormatPeso(analysis.AverageExpense!.Value)}, and the threshold was {FormatPeso(analysis.LargeTransactionThreshold!.Value)}.",
        _ => "I don't have enough information to answer that question."
    };

    private static string FormatPeso(decimal amount) =>
        $"₱{amount.ToString("N2", PhilippineCulture)}";

    private static string FormatPercentage(decimal percentage) =>
        $"{percentage.ToString("0.#", PhilippineCulture)}%";

    private static string BuildLargeExpenseResponse(
        MonthlyTransactionAnalysis analysis,
        MonthlyTransactionFact expense)
    {
        var response =
            $"Your unusually large expense in {analysis.MonthName} was {FormatPeso(expense.Amount)} at {expense.Merchant} " +
            $"in {expense.CategoryName}, paid from {expense.AccountName} on {expense.Date:yyyy-MM-dd}. " +
            $"The monthly average was {FormatPeso(analysis.AverageExpense!.Value)}, and the large-transaction threshold was {FormatPeso(analysis.LargeTransactionThreshold!.Value)}.";

        return string.IsNullOrWhiteSpace(expense.Description)
            ? response
            : $"{response} Description: {expense.Description}";
    }
}
