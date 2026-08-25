using System.Globalization;
using static Kwenta.Services.SpendingComparisonAnalysisService;

namespace Kwenta.Services;

public static class SpendingComparisonChatResponseBuilder
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public static string BuildDeterministicResponse(
        FinancialChatQuestion question,
        SpendingComparisonAnalysis analysis) => question switch
    {
        FinancialChatQuestion.PreviousMonthSpending when analysis.PreviousMonth.ExpenseCount == 0 =>
            $"You have no expenses recorded for {analysis.PreviousMonth.MonthName}, so your spending was ₱0.00.",
        FinancialChatQuestion.PreviousMonthSpending =>
            $"You spent {FormatPeso(analysis.PreviousMonth.Spending)} in {analysis.PreviousMonth.MonthName}.",
        FinancialChatQuestion.SpendingComparison when
            analysis.CurrentMonth.ExpenseCount == 0 &&
            analysis.PreviousMonth.ExpenseCount == 0 =>
            $"You have no expenses recorded for {analysis.PreviousMonth.MonthName} or {analysis.CurrentMonth.MonthName}. Spending is unchanged at ₱0.00.",
        FinancialChatQuestion.SpendingComparison when analysis.PercentageChange is null =>
            $"Your spending {DescribeResult(analysis.Result)}, from {FormatPeso(analysis.PreviousMonth.Spending)} in {analysis.PreviousMonth.MonthName} to {FormatPeso(analysis.CurrentMonth.Spending)} in {analysis.CurrentMonth.MonthName}. Difference: {FormatSignedPeso(analysis.Difference)}. A percentage change is not meaningful because previous-month spending was zero.",
        FinancialChatQuestion.SpendingComparison =>
            $"Your spending {DescribeResult(analysis.Result)}, from {FormatPeso(analysis.PreviousMonth.Spending)} in {analysis.PreviousMonth.MonthName} to {FormatPeso(analysis.CurrentMonth.Spending)} in {analysis.CurrentMonth.MonthName}. Difference: {FormatSignedPeso(analysis.Difference)} ({FormatSignedPercentage(analysis.PercentageChange!.Value)}).",
        _ => "I don't have enough spending information to answer that question."
    };

    private static string DescribeResult(SpendingChangeResult result) => result switch
    {
        SpendingChangeResult.Increased => "increased",
        SpendingChangeResult.Decreased => "decreased",
        _ => "was unchanged"
    };

    private static string FormatPeso(decimal amount) =>
        $"₱{amount.ToString("N2", PhilippineCulture)}";

    private static string FormatSignedPeso(decimal amount) => amount switch
    {
        > 0 => $"+₱{amount.ToString("N2", PhilippineCulture)}",
        < 0 => $"-₱{Math.Abs(amount).ToString("N2", PhilippineCulture)}",
        _ => "₱0.00"
    };

    private static string FormatSignedPercentage(decimal percentage) => percentage switch
    {
        > 0 => $"+{percentage.ToString("0.#", PhilippineCulture)}%",
        < 0 => $"{percentage.ToString("0.#", PhilippineCulture)}%",
        _ => "0%"
    };
}
