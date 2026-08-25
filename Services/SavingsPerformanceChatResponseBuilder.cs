using System.Globalization;
using static Kwenta.Services.SavingsPerformanceAnalysisService;

namespace Kwenta.Services;

public static class SavingsPerformanceChatResponseBuilder
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public static string BuildDeterministicResponse(
        FinancialChatQuestion question,
        SavingsPerformanceAnalysis analysis) => question switch
    {
        FinancialChatQuestion.CurrentMonthSavings when analysis.CurrentMonth.TransactionCount == 0 =>
            $"You have no income or expenses recorded for {analysis.CurrentMonth.MonthName}, so your monthly savings are ₱0.00.",
        FinancialChatQuestion.CurrentMonthSavings =>
            $"Your savings for {analysis.CurrentMonth.MonthName} are {FormatPeso(analysis.CurrentMonth.Savings)}: {FormatPeso(analysis.CurrentMonth.Income)} income minus {FormatPeso(analysis.CurrentMonth.Expenses)} expenses.",
        FinancialChatQuestion.PreviousMonthSavings when analysis.PreviousMonth.TransactionCount == 0 =>
            $"You have no income or expenses recorded for {analysis.PreviousMonth.MonthName}, so your monthly savings were ₱0.00.",
        FinancialChatQuestion.PreviousMonthSavings =>
            $"Your savings for {analysis.PreviousMonth.MonthName} were {FormatPeso(analysis.PreviousMonth.Savings)}: {FormatPeso(analysis.PreviousMonth.Income)} income minus {FormatPeso(analysis.PreviousMonth.Expenses)} expenses.",
        FinancialChatQuestion.SavingsComparison when
            analysis.CurrentMonth.TransactionCount == 0 &&
            analysis.PreviousMonth.TransactionCount == 0 =>
            $"You have no income or expenses recorded for {analysis.PreviousMonth.MonthName} or {analysis.CurrentMonth.MonthName}. Savings are unchanged at ₱0.00.",
        FinancialChatQuestion.SavingsComparison when analysis.PercentageChange is null =>
            $"Your savings {DescribeResult(analysis.Result)} by {FormatSignedPeso(analysis.SavingsDifference)}, from {FormatPeso(analysis.PreviousMonth.Savings)} in {analysis.PreviousMonth.MonthName} to {FormatPeso(analysis.CurrentMonth.Savings)} in {analysis.CurrentMonth.MonthName}. A percentage change is not meaningful because previous-month savings were zero.",
        FinancialChatQuestion.SavingsComparison =>
            $"Your savings {DescribeResult(analysis.Result)} by {FormatSignedPeso(analysis.SavingsDifference)} ({FormatSignedPercentage(analysis.PercentageChange!.Value)}), from {FormatPeso(analysis.PreviousMonth.Savings)} in {analysis.PreviousMonth.MonthName} to {FormatPeso(analysis.CurrentMonth.Savings)} in {analysis.CurrentMonth.MonthName}.",
        _ => "I don't have enough savings information to answer that question."
    };

    private static string DescribeResult(SavingsChangeResult result) => result switch
    {
        SavingsChangeResult.Improved => "improved",
        SavingsChangeResult.Declined => "declined",
        _ => "were unchanged"
    };

    private static string FormatPeso(decimal amount) => amount < 0
        ? $"-₱{Math.Abs(amount).ToString("N2", PhilippineCulture)}"
        : $"₱{amount.ToString("N2", PhilippineCulture)}";

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
