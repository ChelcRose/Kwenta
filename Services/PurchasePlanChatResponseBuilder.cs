using System.Globalization;
using static Kwenta.Services.PurchasePlanAnalysisService;

namespace Kwenta.Services;

public static class PurchasePlanChatResponseBuilder
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public static string BuildDeterministicResponse(PurchasePlanAnalysis plan)
    {
        if (plan.Affordability.ActiveAccountCount == 0)
        {
            return $"I can't create a reliable plan for {plan.Purchase.Name} yet because you have no active financial accounts.";
        }

        if (plan.TargetGap <= 0)
        {
            return $"Your {FormatPeso(plan.Purchase.Amount)} target for {plan.Purchase.Name} is already covered by the conservative plan amount of {FormatPeso(plan.AvailableAmountForPlan)}. No savings timeline is needed, and no money was moved or reserved.";
        }

        if (plan.SuggestedMonthlyContribution <= 0 || plan.MonthsNeeded is null)
        {
            return $"You still need {FormatPeso(plan.TargetGap)} for {plan.Purchase.Name}. Kwenta can't estimate a reliable monthly contribution or timeframe because your current-month savings are {FormatPeso(plan.Affordability.SavedThisMonth)}.";
        }

        var affordabilityNote = plan.Affordability.Recommendation == AffordabilityRecommendation.Affordable
            ? "The existing affordability check says the purchase is affordable, but this plan avoids assuming your full account balance should be used. "
            : string.Empty;

        return $"{affordabilityNote}For {plan.Purchase.Name}, the conservative amount currently available for the plan is {FormatPeso(plan.AvailableAmountForPlan)}, leaving {FormatPeso(plan.TargetGap)}. Contributing {FormatPeso(plan.SuggestedMonthlyContribution)} per month would take about {plan.MonthsNeeded:0} month(s), based on current cash flow. This is an estimate, not a guarantee, and no money was moved or reserved.";
    }

    private static string FormatPeso(decimal amount) => amount < 0
        ? $"-₱{Math.Abs(amount).ToString("N2", PhilippineCulture)}"
        : $"₱{amount.ToString("N2", PhilippineCulture)}";
}
