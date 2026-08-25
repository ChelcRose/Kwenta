using System.Globalization;
using static Kwenta.Services.AffordabilityAnalysisService;

namespace Kwenta.Services;

public static class AffordabilityChatResponseBuilder
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public static string BuildDeterministicResponse(AffordabilityAnalysis analysis)
    {
        if (analysis.ActiveAccountCount == 0)
        {
            return $"I can't establish whether {analysis.Purchase.Name} at {FormatPeso(analysis.Purchase.Amount)} is affordable because you have no active financial accounts. Your calculated available balance is ₱0.00.";
        }

        return analysis.Recommendation switch
        {
            AffordabilityRecommendation.No =>
                $"No — {analysis.Purchase.Name} costs {FormatPeso(analysis.Purchase.Amount)}, which exceeds your calculated available balance of {FormatPeso(analysis.TotalAvailableBalance)}.",
            AffordabilityRecommendation.Caution =>
                $"Caution — {analysis.Purchase.Name} at {FormatPeso(analysis.Purchase.Amount)} fits your calculated available balance of {FormatPeso(analysis.TotalAvailableBalance)}, but {DescribeCaution(analysis)}",
            _ =>
                $"Affordable — {analysis.Purchase.Name} at {FormatPeso(analysis.Purchase.Amount)} is within your calculated available balance of {FormatPeso(analysis.TotalAvailableBalance)} and your current monthly position is positive."
        };
    }

    private static string DescribeCaution(AffordabilityAnalysis analysis)
    {
        if (analysis.SavedThisMonth <= 0)
        {
            return $"your saved-this-month amount is {FormatPeso(analysis.SavedThisMonth)}. Consider the tradeoff before buying.";
        }

        return $"it exceeds your total remaining current-month budget of {FormatPeso(analysis.TotalRemainingBudget)}. Consider the tradeoff before buying.";
    }

    private static string FormatPeso(decimal amount) => amount < 0
        ? $"-₱{Math.Abs(amount).ToString("N2", PhilippineCulture)}"
        : $"₱{amount.ToString("N2", PhilippineCulture)}";
}
