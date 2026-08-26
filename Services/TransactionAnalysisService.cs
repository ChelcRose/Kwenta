using System.Globalization;
using System.Text;
using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed class TransactionAnalysisService(ApplicationDbContext db)
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");

    public async Task<MonthlyTransactionAnalysis> GetCurrentMonthAsync(
        string userId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var transactions = await db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId &&
                ((transaction.Category.IsDefault && transaction.Category.UserId == null) ||
                 (!transaction.Category.IsDefault && transaction.Category.UserId == userId)) &&
                transaction.Date >= monthStart &&
                transaction.Date < nextMonthStart &&
                (transaction.Type == TransactionType.Expense ||
                 transaction.Type == TransactionType.Income))
            .Select(transaction => new MonthlyTransactionFact(
                transaction.Id,
                transaction.Type,
                transaction.Amount,
                transaction.Date,
                transaction.Merchant,
                transaction.Description,
                transaction.CategoryId,
                transaction.Category.Name,
                transaction.FinancialAccount.Name))
            .ToListAsync(cancellationToken);

        var totalExpenses = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);
        var totalIncome = transactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => transaction.Amount);

        var expenses = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .ToList();

        var expenseCategories = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .GroupBy(transaction => new { transaction.CategoryId, transaction.CategoryName })
            .Select(group => new CategoryExpenseTotal(
                group.Key.CategoryId,
                group.Key.CategoryName,
                group.Sum(transaction => transaction.Amount)))
            .OrderByDescending(category => category.Amount)
            .ThenBy(category => category.CategoryName, StringComparer.Ordinal)
            .ThenBy(category => category.CategoryId)
            .ToList();

        var largestExpense = expenses
            .OrderByDescending(transaction => transaction.Amount)
            .ThenByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.Id)
            .FirstOrDefault();

        var averageExpense = expenses.Count >= 3
            ? expenses.Average(transaction => transaction.Amount)
            : (decimal?)null;
        var largeTransactionThreshold = averageExpense * 2;
        var unusuallyLargeExpense = largeTransactionThreshold is { } threshold
            ? expenses
                .Where(transaction => transaction.Amount >= threshold)
                .OrderByDescending(transaction => transaction.Amount)
                .ThenByDescending(transaction => transaction.Date)
                .ThenByDescending(transaction => transaction.Id)
                .FirstOrDefault()
            : null;

        return new MonthlyTransactionAnalysis(
            monthStart,
            transactions.Count,
            expenses.Count,
            totalExpenses,
            totalIncome,
            expenseCategories,
            largestExpense,
            averageExpense,
            largeTransactionThreshold,
            unusuallyLargeExpense);
    }

    public sealed record MonthlyTransactionFact(
        int Id,
        TransactionType Type,
        decimal Amount,
        DateOnly Date,
        string Merchant,
        string? Description,
        int CategoryId,
        string CategoryName,
        string AccountName);

    public sealed record CategoryExpenseTotal(int CategoryId, string CategoryName, decimal Amount);

    public sealed record MonthlyTransactionAnalysis(
        DateOnly MonthStart,
        int TransactionCount,
        int ExpenseCount,
        decimal TotalExpenses,
        decimal TotalIncome,
        IReadOnlyList<CategoryExpenseTotal> ExpenseCategories,
        MonthlyTransactionFact? LargestExpense,
        decimal? AverageExpense,
        decimal? LargeTransactionThreshold,
        MonthlyTransactionFact? UnusuallyLargeExpense)
    {
        public string MonthName => MonthStart.ToString("MMMM yyyy", PhilippineCulture);

        public CategoryExpenseTotal? HighestSpendingCategory => ExpenseCategories.FirstOrDefault();

        public decimal? HighestSpendingCategoryPercentage =>
            HighestSpendingCategory is { } category && TotalExpenses > 0
                ? category.Amount / TotalExpenses * 100
                : null;

        public bool HasEnoughLargeTransactionHistory => ExpenseCount >= 3;

        public string ToLargeTransactionAiContext()
        {
            var expense = UnusuallyLargeExpense;
            if (AverageExpense is null || LargeTransactionThreshold is null || expense is null)
            {
                throw new InvalidOperationException(
                    "Large-transaction AI context requires a qualifying deterministic result.");
            }

            var context = new StringBuilder()
                .AppendLine($"Current month: {MonthName}")
                .AppendLine($"Expense count: {ExpenseCount}")
                .AppendLine($"Average expense: {FormatPeso(AverageExpense.Value)}")
                .AppendLine($"Large-transaction threshold: {FormatPeso(LargeTransactionThreshold.Value)}")
                .AppendLine("Unusually large expense:")
                .AppendLine($"Amount: {FormatPeso(expense.Amount)}")
                .AppendLine($"Merchant: {expense.Merchant}")
                .AppendLine($"Category: {expense.CategoryName}")
                .AppendLine($"Account: {expense.AccountName}")
                .AppendLine($"Date: {expense.Date:yyyy-MM-dd}");

            if (!string.IsNullOrWhiteSpace(expense.Description))
            {
                context.AppendLine($"Description: {expense.Description}");
            }

            return context.ToString().TrimEnd();
        }

        public string ToTopSpendingCategoryAiContext()
        {
            var topCategory = HighestSpendingCategory;
            if (topCategory is null || HighestSpendingCategoryPercentage is null)
            {
                return $"Current month: {MonthName}\nTotal spending: ₱0.00\nTop spending category: None";
            }

            return new StringBuilder()
                .AppendLine($"Current month: {MonthName}")
                .AppendLine($"Total spending: {FormatPeso(TotalExpenses)}")
                .AppendLine($"Top spending category: {topCategory.CategoryName}")
                .AppendLine($"Top category spending: {FormatPeso(topCategory.Amount)}")
                .AppendLine($"Share of total spending: {FormatPercentage(HighestSpendingCategoryPercentage.Value)}")
                .ToString()
                .TrimEnd();
        }

        public string ToAiContext(bool includeLargestExpenseDescription)
        {
            var context = new StringBuilder()
                .AppendLine($"Current month: {MonthName}")
                .AppendLine($"Transaction count: {TransactionCount}")
                .AppendLine($"Total expenses: {FormatPeso(TotalExpenses)}")
                .AppendLine($"Total income: {FormatPeso(TotalIncome)}")
                .AppendLine("Expense totals by category:");

            if (ExpenseCategories.Count == 0)
            {
                context.AppendLine("- None");
            }
            else
            {
                foreach (var category in ExpenseCategories)
                {
                    context.AppendLine($"- {category.CategoryName}: {FormatPeso(category.Amount)}");
                }
            }

            var highestCategory = HighestSpendingCategory;
            context.AppendLine(highestCategory is null
                ? "Highest-spending expense category: None"
                : $"Highest-spending expense category: {highestCategory.CategoryName} — {FormatPeso(highestCategory.Amount)}");

            context.AppendLine(LargestExpense is null
                ? "Largest expense: None"
                : $"Largest expense: {FormatPeso(LargestExpense.Amount)} — {LargestExpense.Merchant} — {LargestExpense.CategoryName} — {LargestExpense.AccountName} — {LargestExpense.Date:yyyy-MM-dd}");

            if (includeLargestExpenseDescription &&
                !string.IsNullOrWhiteSpace(LargestExpense?.Description))
            {
                context.AppendLine($"Largest expense description: {LargestExpense.Description}");
            }

            return context.ToString().TrimEnd();
        }

        private static string FormatPeso(decimal amount) =>
            $"₱{amount.ToString("N2", PhilippineCulture)}";

        private static string FormatPercentage(decimal percentage) =>
            $"{percentage.ToString("0.#", PhilippineCulture)}%";
    }
}
