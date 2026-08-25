using System.Globalization;
using System.Text;
using static Kwenta.Services.AffordabilityAnalysisService;

namespace Kwenta.Services;

public sealed class PurchasePlanAnalysisService(AffordabilityAnalysisService affordabilityAnalysis)
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");
    private const decimal ContributionRate = 0.50m;
    private const int SavingsGoalsInPlanContext = 3;

    public async Task<PurchasePlanAnalysis> GetAsync(
        string userId,
        PurchaseRequest purchase,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var affordability = await affordabilityAnalysis.GetAsync(
            userId,
            purchase,
            today,
            cancellationToken);

        var positiveBalance = Math.Max(affordability.TotalAvailableBalance, 0);
        var positiveMonthlySavings = Math.Max(affordability.SavedThisMonth, 0);
        var availableAmountForPlan = Math.Min(positiveBalance, positiveMonthlySavings);
        var targetGap = purchase.Amount - availableAmountForPlan;

        var contribution = affordability.SavedThisMonth > 0
            ? decimal.Round(
                affordability.SavedThisMonth * ContributionRate,
                2,
                MidpointRounding.AwayFromZero)
            : 0;

        decimal? monthsNeeded = targetGap > 0 && contribution > 0
            ? decimal.Ceiling(targetGap / contribution)
            : null;

        var matchingGoals = affordability.SavingsGoals
            .Where(goal => NamesOverlap(goal.Name, purchase.Name))
            .Take(SavingsGoalsInPlanContext)
            .ToList();

        return new PurchasePlanAnalysis(
            purchase,
            affordability,
            availableAmountForPlan,
            targetGap,
            contribution,
            monthsNeeded,
            matchingGoals);
    }

    private static bool NamesOverlap(string goalName, string purchaseName)
    {
        var normalizedGoal = NormalizeName(goalName);
        var normalizedPurchase = NormalizeName(purchaseName);

        return normalizedGoal == normalizedPurchase;
    }

    private static string NormalizeName(string value) =>
        string.Join(' ', value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToUpperInvariant();

    public sealed record PurchasePlanAnalysis(
        PurchaseRequest Purchase,
        AffordabilityAnalysis Affordability,
        decimal AvailableAmountForPlan,
        decimal TargetGap,
        decimal SuggestedMonthlyContribution,
        decimal? MonthsNeeded,
        IReadOnlyList<SavingsGoalFact> MatchingSavingsGoals)
    {
        public string ToAiContext()
        {
            var context = new StringBuilder()
                .AppendLine("Purchase plan:")
                .AppendLine($"Target: {Purchase.Name}")
                .AppendLine($"Target amount: {FormatPeso(Purchase.Amount)}")
                .AppendLine()
                .AppendLine("Current financial position:")
                .AppendLine($"Active account count: {Affordability.ActiveAccountCount}")
                .AppendLine($"Available balance: {FormatPeso(Affordability.TotalAvailableBalance)}")
                .AppendLine($"Current month: {Affordability.MonthName}")
                .AppendLine($"Current-month income: {FormatPeso(Affordability.CurrentMonthIncome)}")
                .AppendLine($"Current-month expenses: {FormatPeso(Affordability.CurrentMonthExpenses)}")
                .AppendLine($"Current-month savings: {FormatPeso(Affordability.SavedThisMonth)}")
                .AppendLine($"Existing affordability recommendation: {Affordability.Recommendation}")
                .AppendLine()
                .AppendLine("Calculated plan:")
                .AppendLine($"Amount considered available for plan: {FormatPeso(AvailableAmountForPlan)}")
                .AppendLine($"Target gap: {FormatPeso(TargetGap)}")
                .AppendLine(SuggestedMonthlyContribution > 0
                    ? $"Suggested monthly contribution: {FormatPeso(SuggestedMonthlyContribution)}"
                    : "Suggested monthly contribution: Unavailable")
                .AppendLine(MonthsNeeded is { } months
                    ? $"Estimated timeframe: {months:0} month(s)"
                    : TargetGap <= 0
                        ? "Estimated timeframe: Not needed; the target is covered by the conservative available amount."
                        : "Estimated timeframe: Unavailable from current monthly cash flow.")
                .AppendLine("Deterministic note: The timeframe uses current cash flow as a planning estimate, not a guarantee. No money was moved or reserved.");

            if (MatchingSavingsGoals.Count > 0)
            {
                context.AppendLine().AppendLine("Matching savings-goal context (not counted as separate available cash):");
                foreach (var goal in MatchingSavingsGoals)
                {
                    context
                        .AppendLine($"- {goal.Name}")
                        .AppendLine($"  Target: {FormatPeso(goal.TargetAmount)}")
                        .AppendLine($"  Saved: {FormatPeso(goal.CurrentSavedAmount)}")
                        .AppendLine($"  Remaining: {FormatPeso(goal.Remaining)}");
                }
            }

            return context.ToString().TrimEnd();
        }

        private static string FormatPeso(decimal amount) => amount < 0
            ? $"-₱{Math.Abs(amount).ToString("N2", PhilippineCulture)}"
            : $"₱{amount.ToString("N2", PhilippineCulture)}";
    }
}
