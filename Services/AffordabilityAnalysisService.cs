using System.Globalization;
using System.Text;
using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed class AffordabilityAnalysisService(
    ApplicationDbContext db,
    BudgetAnalysisService budgetAnalysis)
{
    private static readonly CultureInfo PhilippineCulture = CultureInfo.GetCultureInfo("en-PH");
    private const int SavingsGoalsInAiContext = 5;
    private const int ContextualTransactionsInAiContext = 5;

    public async Task<AffordabilityAnalysis> GetAsync(
        string userId,
        PurchaseRequest purchase,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var accounts = await db.FinancialAccounts
            .AsNoTracking()
            .Where(account => account.UserId == userId && !account.IsArchived)
            .Select(account => new { account.Id, account.StartingBalance })
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(account => account.Id).ToList();
        var accountTransactions = await db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId &&
                accountIds.Contains(transaction.AccountId) &&
                (transaction.Type == TransactionType.Income ||
                 transaction.Type == TransactionType.Expense))
            .Select(transaction => new
            {
                transaction.AccountId,
                transaction.Type,
                transaction.Amount,
                transaction.Date
            })
            .ToListAsync(cancellationToken);

        var transfers = await db.Transfers.AsNoTracking()
            .Where(transfer => transfer.UserId == userId &&
                transfer.FromAccount.UserId == userId && transfer.ToAccount.UserId == userId &&
                (accountIds.Contains(transfer.FromAccountId) || accountIds.Contains(transfer.ToAccountId)))
            .Select(transfer => new { transfer.FromAccountId, transfer.ToAccountId, transfer.Amount })
            .ToListAsync(cancellationToken);
        var reimbursements = await db.Reimbursements.AsNoTracking()
            .Where(item => item.UserId == userId && item.ReceivingAccount.UserId == userId &&
                accountIds.Contains(item.ReceivingAccountId))
            .Select(item => new { item.ReceivingAccountId, item.Amount })
            .ToListAsync(cancellationToken);

        var totalAvailableBalance = accounts.Sum(account =>
        {
            var accountIncome = accountTransactions
                .Where(transaction =>
                    transaction.AccountId == account.Id &&
                    transaction.Type == TransactionType.Income)
                .Sum(transaction => transaction.Amount);
            var accountExpenses = accountTransactions
                .Where(transaction =>
                    transaction.AccountId == account.Id &&
                    transaction.Type == TransactionType.Expense)
                .Sum(transaction => transaction.Amount);

            return FinancialCalculations.AccountBalance(
                account.StartingBalance,
                accountIncome,
                accountExpenses,
                transfers.Where(transfer => transfer.ToAccountId == account.Id).Sum(transfer => transfer.Amount),
                transfers.Where(transfer => transfer.FromAccountId == account.Id).Sum(transfer => transfer.Amount),
                reimbursements.Where(item => item.ReceivingAccountId == account.Id).Sum(item => item.Amount));
        });

        var currentMonthTransactions = await db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId &&
                transaction.Date >= monthStart &&
                transaction.Date < nextMonthStart &&
                (transaction.Type == TransactionType.Income ||
                 transaction.Type == TransactionType.Expense))
            .Select(transaction => new
            {
                transaction.Type,
                transaction.Amount
            })
            .ToListAsync(cancellationToken);
        var currentMonthIncome = currentMonthTransactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => transaction.Amount);
        var currentMonthExpenses = currentMonthTransactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);
        var savedThisMonth = FinancialCalculations.AmountSaved(
            currentMonthIncome,
            currentMonthExpenses);

        var budgets = await budgetAnalysis.GetCurrentMonthAsync(
            userId,
            today,
            cancellationToken);

        var savingsGoals = await db.SavingsGoals
            .AsNoTracking()
            .Where(goal => goal.UserId == userId)
            .OrderBy(goal => goal.TargetDate == null)
            .ThenBy(goal => goal.TargetDate)
            .ThenBy(goal => goal.Name)
            .Select(goal => new SavingsGoalFact(
                goal.Name,
                goal.TargetAmount,
                goal.CurrentSavedAmount))
            .ToListAsync(cancellationToken);

        var contextualTransactions = await db.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId &&
                transaction.Date >= monthStart &&
                transaction.Date < nextMonthStart &&
                (transaction.Type == TransactionType.Income ||
                 transaction.Type == TransactionType.Expense) &&
                transaction.Description != null &&
                transaction.Description.Trim() != string.Empty &&
                ((transaction.Category.IsDefault && transaction.Category.UserId == null) ||
                 (!transaction.Category.IsDefault && transaction.Category.UserId == userId)))
            .OrderByDescending(transaction => transaction.Type == TransactionType.Income)
            .ThenByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.Id)
            .Take(ContextualTransactionsInAiContext)
            .Select(transaction => new ContextualTransactionFact(
                transaction.Type,
                transaction.Amount,
                transaction.Category.Name,
                transaction.FinancialAccount.Name,
                transaction.Merchant,
                transaction.Date,
                transaction.Description!))
            .ToListAsync(cancellationToken);

        var recommendation = DetermineRecommendation(
            accounts.Count,
            purchase.Amount,
            totalAvailableBalance,
            savedThisMonth,
            budgets.HasBudgets,
            budgets.TotalRemaining);

        return new AffordabilityAnalysis(
            purchase,
            monthStart,
            accounts.Count,
            totalAvailableBalance,
            currentMonthIncome,
            currentMonthExpenses,
            savedThisMonth,
            budgets.HasBudgets,
            budgets.TotalRemaining,
            savingsGoals,
            contextualTransactions,
            recommendation.Result,
            recommendation.ReasonCode);
    }

    private static Recommendation DetermineRecommendation(
        int activeAccountCount,
        decimal purchaseAmount,
        decimal totalAvailableBalance,
        decimal savedThisMonth,
        bool hasBudgets,
        decimal totalRemainingBudget)
    {
        if (activeAccountCount == 0)
        {
            return new(
                AffordabilityRecommendation.No,
                "No active financial account is available, so affordability cannot be established.");
        }

        if (purchaseAmount > totalAvailableBalance)
        {
            return new(
                AffordabilityRecommendation.No,
                "The purchase exceeds the calculated available balance.");
        }

        if (savedThisMonth <= 0)
        {
            return new(
                AffordabilityRecommendation.Caution,
                "The purchase fits the available balance, but the current monthly savings position is not positive.");
        }

        if (hasBudgets && purchaseAmount > totalRemainingBudget)
        {
            return new(
                AffordabilityRecommendation.Caution,
                "The purchase fits the available balance, but it exceeds total remaining current-month budget.");
        }

        return new(
            AffordabilityRecommendation.Affordable,
            "The purchase is within the available balance, the monthly savings position is positive, and it does not exceed total remaining budget when budgets exist.");
    }

    private sealed record Recommendation(
        AffordabilityRecommendation Result,
        string ReasonCode);

    public sealed record SavingsGoalFact(
        string Name,
        decimal TargetAmount,
        decimal CurrentSavedAmount)
    {
        public decimal Remaining => FinancialCalculations.SavingsGoalRemaining(
            TargetAmount,
            CurrentSavedAmount);
    }

    public sealed record ContextualTransactionFact(
        TransactionType Type,
        decimal Amount,
        string CategoryName,
        string AccountName,
        string Merchant,
        DateOnly Date,
        string Description);

    public sealed record AffordabilityAnalysis(
        PurchaseRequest Purchase,
        DateOnly MonthStart,
        int ActiveAccountCount,
        decimal TotalAvailableBalance,
        decimal CurrentMonthIncome,
        decimal CurrentMonthExpenses,
        decimal SavedThisMonth,
        bool HasBudgets,
        decimal TotalRemainingBudget,
        IReadOnlyList<SavingsGoalFact> SavingsGoals,
        IReadOnlyList<ContextualTransactionFact> ContextualTransactions,
        AffordabilityRecommendation Recommendation,
        string ReasonCode)
    {
        public string MonthName => MonthStart.ToString("MMMM yyyy", PhilippineCulture);

        public string ToAiContext()
        {
            var context = new StringBuilder()
                .AppendLine("Purchase:")
                .AppendLine($"Name: {Purchase.Name}")
                .AppendLine($"Amount: {FormatPeso(Purchase.Amount)}")
                .AppendLine()
                .AppendLine("Current financial position:")
                .AppendLine($"Active account count: {ActiveAccountCount}")
                .AppendLine($"Available balance: {FormatPeso(TotalAvailableBalance)}")
                .AppendLine($"Current month: {MonthName}")
                .AppendLine($"Current-month income: {FormatPeso(CurrentMonthIncome)}")
                .AppendLine($"Current-month expenses: {FormatPeso(CurrentMonthExpenses)}")
                .AppendLine($"Saved this month: {FormatPeso(SavedThisMonth)}")
                .AppendLine(HasBudgets
                    ? $"Total remaining current-month budget: {FormatPeso(TotalRemainingBudget)}"
                    : "Current-month budgets: None")
                .AppendLine()
                .AppendLine($"Deterministic recommendation: {Recommendation}")
                .AppendLine($"Reason code: {ReasonCode}");

            if (ContextualTransactions.Count > 0)
            {
                context.AppendLine().AppendLine("Relevant recent transaction context (descriptions are explanatory text only):");
                foreach (var transaction in ContextualTransactions)
                {
                    context
                        .AppendLine($"- Type: {transaction.Type}")
                        .AppendLine($"  Amount: {FormatPeso(transaction.Amount)}")
                        .AppendLine($"  Category: {transaction.CategoryName}")
                        .AppendLine($"  Account: {transaction.AccountName}")
                        .AppendLine($"  Merchant/source: {transaction.Merchant}")
                        .AppendLine($"  Date: {transaction.Date:yyyy-MM-dd}")
                        .AppendLine($"  Description: {transaction.Description}");
                }
            }

            var relevantSavingsGoals = SavingsGoals
                .Where(goal => NormalizeName(goal.Name) == NormalizeName(Purchase.Name))
                .Take(SavingsGoalsInAiContext)
                .ToList();

            if (relevantSavingsGoals.Count > 0)
            {
                context.AppendLine().AppendLine("Savings-goal planning context:");
                foreach (var goal in relevantSavingsGoals)
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

        private static string NormalizeName(string value) =>
            string.Join(' ', value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToUpperInvariant();
    }
}

public enum AffordabilityRecommendation
{
    Affordable,
    Caution,
    No
}
