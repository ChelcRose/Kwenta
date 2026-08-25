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

        var expenseCategories = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .GroupBy(transaction => new { transaction.CategoryId, transaction.CategoryName })
            .Select(group => new CategoryExpenseTotal(
                group.Key.CategoryId,
                group.Key.CategoryName,
                group.Sum(transaction => transaction.Amount)))
            .OrderByDescending(category => category.Amount)
            .ThenBy(category => category.CategoryName)
            .ToList();

        var largestExpense = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .OrderByDescending(transaction => transaction.Amount)
            .ThenByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.Id)
            .FirstOrDefault();

        return new MonthlyTransactionAnalysis(
            monthStart,
            transactions.Count,
            totalExpenses,
            totalIncome,
            expenseCategories,
            largestExpense);
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
        decimal TotalExpenses,
        decimal TotalIncome,
        IReadOnlyList<CategoryExpenseTotal> ExpenseCategories,
        MonthlyTransactionFact? LargestExpense)
    {
        public string MonthName => MonthStart.ToString("MMMM yyyy", PhilippineCulture);

        public CategoryExpenseTotal? HighestSpendingCategory => ExpenseCategories.FirstOrDefault();

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
    }
}
