using System.Globalization;
using System.Text;
using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed class SavingsPerformanceAnalysisService(ApplicationDbContext db)
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public async Task<SavingsPerformanceAnalysis> GetAsync(
        string userId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var nextMonthStart = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        var transactions = await db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId &&
                transaction.Date >= previousMonthStart &&
                transaction.Date < nextMonthStart &&
                (transaction.Type == TransactionType.Income ||
                 transaction.Type == TransactionType.Expense))
            .Select(transaction => new MonthlyCashFlowFact(
                transaction.Date,
                transaction.Type,
                transaction.Amount))
            .ToListAsync(cancellationToken);

        var currentMonth = CalculateMonth(
            currentMonthStart,
            transactions.Where(transaction => transaction.Date >= currentMonthStart));
        var previousMonth = CalculateMonth(
            previousMonthStart,
            transactions.Where(transaction => transaction.Date < currentMonthStart));

        return new SavingsPerformanceAnalysis(currentMonth, previousMonth);
    }

    private static MonthlySavingsFact CalculateMonth(
        DateOnly monthStart,
        IEnumerable<MonthlyCashFlowFact> transactions)
    {
        var monthTransactions = transactions.ToList();
        var income = monthTransactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => transaction.Amount);
        var expenses = monthTransactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);

        return new MonthlySavingsFact(
            monthStart,
            monthTransactions.Count,
            income,
            expenses,
            FinancialCalculations.AmountSaved(income, expenses));
    }

    private sealed record MonthlyCashFlowFact(
        DateOnly Date,
        TransactionType Type,
        decimal Amount);

    public sealed record MonthlySavingsFact(
        DateOnly MonthStart,
        int TransactionCount,
        decimal Income,
        decimal Expenses,
        decimal Savings)
    {
        public string MonthName => MonthStart.ToString("MMMM yyyy", PhilippineCulture);
    }

    public sealed record SavingsPerformanceAnalysis(
        MonthlySavingsFact CurrentMonth,
        MonthlySavingsFact PreviousMonth)
    {
        public decimal SavingsDifference => CurrentMonth.Savings - PreviousMonth.Savings;

        public decimal? PercentageChange => PreviousMonth.Savings == 0
            ? null
            : SavingsDifference / Math.Abs(PreviousMonth.Savings) * 100;

        public SavingsChangeResult Result => SavingsDifference switch
        {
            > 0 => SavingsChangeResult.Improved,
            < 0 => SavingsChangeResult.Declined,
            _ => SavingsChangeResult.Unchanged
        };

        public bool HasDataFor(FinancialChatQuestion question) => question switch
        {
            FinancialChatQuestion.CurrentMonthSavings => CurrentMonth.TransactionCount > 0,
            FinancialChatQuestion.PreviousMonthSavings => PreviousMonth.TransactionCount > 0,
            FinancialChatQuestion.SavingsComparison =>
                CurrentMonth.TransactionCount > 0 || PreviousMonth.TransactionCount > 0,
            _ => false
        };

        public string ToAiContext(FinancialChatQuestion question)
        {
            var context = new StringBuilder();

            switch (question)
            {
                case FinancialChatQuestion.CurrentMonthSavings:
                    AppendMonth(context, "Current month", CurrentMonth);
                    break;

                case FinancialChatQuestion.PreviousMonthSavings:
                    AppendMonth(context, "Previous month", PreviousMonth);
                    break;

                case FinancialChatQuestion.SavingsComparison:
                    AppendMonth(context, "Current month", CurrentMonth);
                    context.AppendLine();
                    AppendMonth(context, "Previous month", PreviousMonth);
                    context
                        .AppendLine()
                        .AppendLine($"Savings difference: {FormatSignedPeso(SavingsDifference)}")
                        .AppendLine($"Percentage change: {(PercentageChange is { } percentage ? FormatSignedPercentage(percentage) : "Unavailable because previous-month savings is zero")}")
                        .AppendLine($"Result: {Result}");
                    break;
            }

            return context.ToString().TrimEnd();
        }

        private static void AppendMonth(
            StringBuilder context,
            string label,
            MonthlySavingsFact month)
        {
            context
                .AppendLine($"{label}: {month.MonthName}")
                .AppendLine($"Income: {FormatPeso(month.Income)}")
                .AppendLine($"Expenses: {FormatPeso(month.Expenses)}")
                .AppendLine($"Savings: {FormatPeso(month.Savings)}");
        }

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

    public enum SavingsChangeResult
    {
        Improved,
        Declined,
        Unchanged
    }
}
