using System.Globalization;
using System.Text;
using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed class BudgetAnalysisService(ApplicationDbContext db)
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public async Task<MonthlyBudgetAnalysis> GetCurrentMonthAsync(
        string userId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var budgets = await db.Budgets
            .AsNoTracking()
            .Where(budget =>
                budget.UserId == userId &&
                budget.Month == today.Month &&
                budget.Year == today.Year &&
                budget.Category.Type == CategoryType.Expense &&
                ((budget.Category.IsDefault && budget.Category.UserId == null) ||
                 (!budget.Category.IsDefault && budget.Category.UserId == userId)))
            .Select(budget => new BudgetDefinition(
                budget.Id,
                budget.CategoryId,
                budget.Category.Name,
                budget.LimitAmount))
            .OrderBy(budget => budget.CategoryName)
            .ToListAsync(cancellationToken);

        if (budgets.Count == 0)
        {
            return MonthlyBudgetAnalysis.Empty(monthStart);
        }

        var categoryIds = budgets.Select(budget => budget.CategoryId).ToList();
        var expenses = await db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId &&
                transaction.Type == TransactionType.Expense &&
                categoryIds.Contains(transaction.CategoryId) &&
                transaction.Date >= monthStart &&
                transaction.Date < nextMonthStart)
            .Select(transaction => new
            {
                transaction.CategoryId,
                transaction.Amount
            })
            .ToListAsync(cancellationToken);

        var spendingByCategory = expenses
            .GroupBy(transaction => transaction.CategoryId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(transaction => transaction.Amount));

        var progress = budgets
            .Select(budget =>
            {
                spendingByCategory.TryGetValue(budget.CategoryId, out var amountSpent);
                return new BudgetProgressFact(
                    budget.Id,
                    budget.CategoryId,
                    budget.CategoryName,
                    budget.LimitAmount,
                    amountSpent);
            })
            .ToList();

        return new MonthlyBudgetAnalysis(monthStart, progress);
    }

    private sealed record BudgetDefinition(
        int Id,
        int CategoryId,
        string CategoryName,
        decimal LimitAmount);

    public sealed record BudgetProgressFact(
        int BudgetId,
        int CategoryId,
        string CategoryName,
        decimal LimitAmount,
        decimal AmountSpent)
    {
        public decimal Remaining => FinancialCalculations.BudgetRemaining(LimitAmount, AmountSpent);

        public decimal PercentageUsed => FinancialCalculations.BudgetPercentageUsed(LimitAmount, AmountSpent);

        public bool IsOverBudget => FinancialCalculations.IsOverBudget(LimitAmount, AmountSpent);
    }

    public sealed record MonthlyBudgetAnalysis(
        DateOnly MonthStart,
        IReadOnlyList<BudgetProgressFact> Budgets)
    {
        public string MonthName => MonthStart.ToString("MMMM yyyy", PhilippineCulture);

        public bool HasBudgets => Budgets.Count > 0;

        public decimal TotalLimit => Budgets.Sum(budget => budget.LimitAmount);

        public decimal TotalSpent => Budgets.Sum(budget => budget.AmountSpent);

        public decimal TotalRemaining => Budgets.Sum(budget => budget.Remaining);

        public IReadOnlyList<BudgetProgressFact> OverBudgetCategories => Budgets
            .Where(budget => budget.IsOverBudget)
            .OrderByDescending(budget => budget.PercentageUsed)
            .ThenBy(budget => budget.CategoryName)
            .ToList();

        public BudgetProgressFact? ClosestToUsedUp => Budgets
            .Where(budget => !budget.IsOverBudget)
            .OrderByDescending(budget => budget.PercentageUsed)
            .ThenBy(budget => budget.CategoryName)
            .FirstOrDefault()
            ?? Budgets
                .OrderBy(budget => budget.PercentageUsed)
                .ThenBy(budget => budget.CategoryName)
                .FirstOrDefault();

        public BudgetCategoryMatch ResolveCategory(string requestedCategoryName)
        {
            var normalizedName = NormalizeCategoryName(requestedCategoryName);
            var matches = Budgets
                .Where(budget => NormalizeCategoryName(budget.CategoryName) == normalizedName)
                .ToList();

            return matches.Count switch
            {
                0 => new(BudgetCategoryMatchStatus.NotFound),
                1 => new(BudgetCategoryMatchStatus.Found, matches[0]),
                _ => new(BudgetCategoryMatchStatus.Ambiguous)
            };
        }

        public string ToAiContext(
            FinancialChatQuestion question,
            BudgetProgressFact? selectedBudget = null)
        {
            var context = new StringBuilder()
                .AppendLine($"Current month: {MonthName}");

            switch (question)
            {
                case FinancialChatQuestion.TotalBudgetRemaining:
                    context
                        .AppendLine($"Total budget limit: {FormatPeso(TotalLimit)}")
                        .AppendLine($"Total budget spent: {FormatPeso(TotalSpent)}")
                        .AppendLine($"Total remaining budget: {FormatPeso(TotalRemaining)}");
                    break;

                case FinancialChatQuestion.OverBudgetCategories:
                    context.AppendLine("Over-budget categories:");
                    AppendBudgets(context, OverBudgetCategories);
                    break;

                case FinancialChatQuestion.CategoryBudgetRemaining when selectedBudget is not null:
                    context.AppendLine("Requested category budget:");
                    AppendBudget(context, selectedBudget);
                    break;

                case FinancialChatQuestion.ClosestBudgetToUsedUp when ClosestToUsedUp is { } closest:
                    context.AppendLine("Budget closest to being used up:");
                    AppendBudget(context, closest);
                    break;
            }

            return context.ToString().TrimEnd();
        }

        public static MonthlyBudgetAnalysis Empty(DateOnly monthStart) => new(monthStart, []);

        private static void AppendBudgets(StringBuilder context, IReadOnlyList<BudgetProgressFact> budgets)
        {
            if (budgets.Count == 0)
            {
                context.AppendLine("- None");
                return;
            }

            foreach (var budget in budgets)
            {
                AppendBudget(context, budget);
            }
        }

        private static void AppendBudget(StringBuilder context, BudgetProgressFact budget)
        {
            context
                .AppendLine($"- Category: {budget.CategoryName}")
                .AppendLine($"  Limit: {FormatPeso(budget.LimitAmount)}")
                .AppendLine($"  Spent: {FormatPeso(budget.AmountSpent)}")
                .AppendLine($"  Remaining: {FormatPeso(budget.Remaining)}")
                .AppendLine($"  Used: {FormatPercentage(budget.PercentageUsed)}")
                .AppendLine($"  Over budget: {(budget.IsOverBudget ? "Yes" : "No")}");
        }

        private static string NormalizeCategoryName(string categoryName) =>
            string.Join(' ', categoryName.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToUpperInvariant();

        private static string FormatPeso(decimal amount) =>
            $"₱{amount.ToString("N2", PhilippineCulture)}";

        private static string FormatPercentage(decimal percentage) =>
            $"{percentage.ToString("0.#", PhilippineCulture)}%";
    }

    public enum BudgetCategoryMatchStatus
    {
        Found,
        NotFound,
        Ambiguous
    }

    public sealed record BudgetCategoryMatch(
        BudgetCategoryMatchStatus Status,
        BudgetProgressFact? Budget = null);
}
