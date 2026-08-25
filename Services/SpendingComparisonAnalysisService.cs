using System.Globalization;
using System.Text;
using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed class SpendingComparisonAnalysisService(ApplicationDbContext db)
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public async Task<SpendingComparisonAnalysis> GetAsync(
        string userId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var nextMonthStart = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        var expenses = await db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId &&
                transaction.Type == TransactionType.Expense &&
                transaction.Date >= previousMonthStart &&
                transaction.Date < nextMonthStart)
            .Select(transaction => new ExpenseFact(
                transaction.Date,
                transaction.Amount))
            .ToListAsync(cancellationToken);

        var currentExpenses = expenses
            .Where(expense => expense.Date >= currentMonthStart)
            .ToList();
        var previousExpenses = expenses
            .Where(expense => expense.Date < currentMonthStart)
            .ToList();

        return new SpendingComparisonAnalysis(
            new MonthlySpendingFact(
                currentMonthStart,
                currentExpenses.Count,
                currentExpenses.Sum(expense => expense.Amount)),
            new MonthlySpendingFact(
                previousMonthStart,
                previousExpenses.Count,
                previousExpenses.Sum(expense => expense.Amount)));
    }

    private sealed record ExpenseFact(DateOnly Date, decimal Amount);

    public sealed record MonthlySpendingFact(
        DateOnly MonthStart,
        int ExpenseCount,
        decimal Spending)
    {
        public string MonthName => MonthStart.ToString("MMMM yyyy", PhilippineCulture);
    }

    public sealed record SpendingComparisonAnalysis(
        MonthlySpendingFact CurrentMonth,
        MonthlySpendingFact PreviousMonth)
    {
        public decimal Difference => CurrentMonth.Spending - PreviousMonth.Spending;

        public decimal? PercentageChange => PreviousMonth.Spending == 0
            ? null
            : Difference / PreviousMonth.Spending * 100;

        public SpendingChangeResult Result => Difference switch
        {
            > 0 => SpendingChangeResult.Increased,
            < 0 => SpendingChangeResult.Decreased,
            _ => SpendingChangeResult.Unchanged
        };

        public bool HasDataFor(FinancialChatQuestion question) => question switch
        {
            FinancialChatQuestion.PreviousMonthSpending => PreviousMonth.ExpenseCount > 0,
            FinancialChatQuestion.SpendingComparison =>
                CurrentMonth.ExpenseCount > 0 || PreviousMonth.ExpenseCount > 0,
            _ => false
        };

        public string ToAiContext(FinancialChatQuestion question)
        {
            var context = new StringBuilder();

            switch (question)
            {
                case FinancialChatQuestion.PreviousMonthSpending:
                    AppendMonth(context, "Previous month", PreviousMonth);
                    break;

                case FinancialChatQuestion.SpendingComparison:
                    AppendMonth(context, "Current month", CurrentMonth);
                    context.AppendLine();
                    AppendMonth(context, "Previous month", PreviousMonth);
                    context
                        .AppendLine()
                        .AppendLine($"Spending difference: {FormatSignedPeso(Difference)}")
                        .AppendLine($"Percentage change: {(PercentageChange is { } percentage ? FormatSignedPercentage(percentage) : "Unavailable because previous-month spending is zero")}")
                        .AppendLine($"Result: Spending {Result}");
                    break;
            }

            return context.ToString().TrimEnd();
        }

        private static void AppendMonth(
            StringBuilder context,
            string label,
            MonthlySpendingFact month) => context
                .AppendLine($"{label}: {month.MonthName}")
                .AppendLine($"Spending: {FormatPeso(month.Spending)}");

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

    public enum SpendingChangeResult
    {
        Increased,
        Decreased,
        Unchanged
    }
}
