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
        FinancialChatQuestion.LargestExpense when analysis.LargestExpense is { } expense =>
            $"Your biggest expense in {analysis.MonthName} was {FormatPeso(expense.Amount)} at {expense.Merchant} in {expense.CategoryName}.",
        FinancialChatQuestion.LargestExpense =>
            $"You have no expenses recorded for {analysis.MonthName}.",
        _ => "I don't have enough information to answer that question."
    };

    private static string FormatPeso(decimal amount) =>
        $"₱{amount.ToString("N2", PhilippineCulture)}";
}
