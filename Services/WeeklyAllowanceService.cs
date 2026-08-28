using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed class WeeklyAllowanceService(ApplicationDbContext db, TransferService transferService)
{
    public async Task<AllowanceOperationResult> CreateAsync(
        string userId,
        decimal amountReceived,
        DateOnly startDate,
        IReadOnlyList<AllowanceAllocationRequest> requestedAllocations,
        bool useExistingFunds = false,
        CancellationToken cancellationToken = default)
    {
        if (amountReceived <= 0 || decimal.Round(amountReceived, 2) != amountReceived)
            return AllowanceOperationResult.Failed("Allowance must be greater than zero with at most two decimal places.");
        if (requestedAllocations.Count == 0)
            return AllowanceOperationResult.Failed("Add at least one account allocation.");
        if (requestedAllocations.Any(item => item.AccountId <= 0 || item.Amount <= 0 || decimal.Round(item.Amount, 2) != item.Amount))
            return AllowanceOperationResult.Failed("Every allocation needs an account and a positive amount with at most two decimal places.");
        if (requestedAllocations.Select(item => item.AccountId).Distinct().Count() != requestedAllocations.Count)
            return AllowanceOperationResult.Failed("Each account may appear only once in an allowance distribution.");
        if (requestedAllocations.Sum(item => item.Amount) != amountReceived)
            return AllowanceOperationResult.Failed("Total allocations must equal the allowance received exactly.");

        await using var databaseTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await db.WeeklyAllowances.AnyAsync(
                    allowance => allowance.UserId == userId && allowance.IsActive,
                    cancellationToken))
                return AllowanceOperationResult.Failed("Close your active weekly allowance before starting another one.");

            var accountIds = requestedAllocations.Select(item => item.AccountId).ToList();
            var accounts = await db.FinancialAccounts
                .Where(account => accountIds.Contains(account.Id) && account.UserId == userId && !account.IsArchived)
                .ToDictionaryAsync(account => account.Id, cancellationToken);
            if (accounts.Count != accountIds.Count)
                return AllowanceOperationResult.Failed("Every allocation must use one of your active financial accounts.");

            if (useExistingFunds)
            {
                var balances = await transferService.GetBalancesAsync(userId, accountIds, cancellationToken);
                var insufficient = requestedAllocations.FirstOrDefault(item =>
                    item.Amount > balances.GetValueOrDefault(item.AccountId));
                if (insufficient is not null)
                    return AllowanceOperationResult.Failed(
                        $"{accounts[insufficient.AccountId].Name} has only ₱{balances.GetValueOrDefault(insufficient.AccountId):N2} available.");
            }

            int? incomeCategoryId = null;
            if (!useExistingFunds)
            {
                incomeCategoryId = await db.Categories
                    .Where(category => category.IsDefault && category.UserId == null &&
                        category.Type == CategoryType.Income && category.Name == "Allowance")
                    .Select(category => (int?)category.Id)
                    .SingleOrDefaultAsync(cancellationToken);
                if (incomeCategoryId is null)
                    return AllowanceOperationResult.Failed("The default Income category needed to record the allowance is unavailable.");
            }

            var allowance = new WeeklyAllowance
            {
                UserId = userId,
                AmountReceived = amountReceived,
                StartDate = startDate,
                EndDate = startDate.AddDays(6),
                IsActive = true
            };

            foreach (var requested in requestedAllocations)
            {
                allowance.Allocations.Add(new WeeklyAllowanceAllocation
                {
                    FinancialAccountId = requested.AccountId,
                    Amount = requested.Amount
                });
                if (!useExistingFunds)
                {
                    db.Transactions.Add(new Transaction
                    {
                        UserId = userId,
                        AccountId = requested.AccountId,
                        CategoryId = incomeCategoryId!.Value,
                        Type = TransactionType.Income,
                        Amount = requested.Amount,
                        Date = startDate,
                        Merchant = "Weekly Allowance",
                        Description = $"Weekly allowance allocation to {accounts[requested.AccountId].Name}."
                    });
                }
            }

            db.WeeklyAllowances.Add(allowance);
            await db.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
            return AllowanceOperationResult.Succeeded(allowance.Id);
        }
        catch (DbUpdateException)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            foreach (var entry in db.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added))
                entry.State = EntityState.Detached;
            return AllowanceOperationResult.Failed("The weekly allowance could not be created. You may already have an active allowance.");
        }
    }

    public async Task<WeeklyAllowanceProgress?> GetActiveProgressAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var allowance = await db.WeeklyAllowances.AsNoTracking()
            .Include(item => item.Allocations)
            .ThenInclude(allocation => allocation.FinancialAccount)
            .SingleOrDefaultAsync(item => item.UserId == userId && item.IsActive, cancellationToken);
        return allowance is null ? null : await BuildProgressAsync(allowance, userId, cancellationToken);
    }

    public async Task<WeeklyAllowanceProgress?> GetLatestClosedProgressAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var allowance = await db.WeeklyAllowances.AsNoTracking()
            .Include(item => item.Allocations)
            .ThenInclude(allocation => allocation.FinancialAccount)
            .Where(item => item.UserId == userId && !item.IsActive)
            .OrderByDescending(item => item.ClosedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return allowance is null ? null : await BuildProgressAsync(allowance, userId, cancellationToken);
    }

    private async Task<WeeklyAllowanceProgress> BuildProgressAsync(
        WeeklyAllowance allowance,
        string userId,
        CancellationToken cancellationToken)
    {
        var expenses = await db.Transactions.AsNoTracking()
            .Include(transaction => transaction.Category)
            .Include(transaction => transaction.FinancialAccount)
            .Include(transaction => transaction.Reimbursements)
            .Where(transaction => transaction.WeeklyAllowanceId == allowance.Id &&
                transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId &&
                transaction.Type == TransactionType.Expense)
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.Id)
            .ToListAsync(cancellationToken);
        return new WeeklyAllowanceProgress(
            allowance,
            expenses.Sum(item => FinancialCalculations.NetExpense(
                item.Amount,
                item.Reimbursements.Sum(reimbursement => reimbursement.Amount))),
            expenses.Take(10).ToList());
    }

    public async Task<AllowanceOperationResult> CloseActiveAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var allowance = await db.WeeklyAllowances.SingleOrDefaultAsync(
            item => item.UserId == userId && item.IsActive,
            cancellationToken);
        if (allowance is null) return AllowanceOperationResult.Failed("No active weekly allowance was found.");

        allowance.IsActive = false;
        allowance.ClosedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return AllowanceOperationResult.Succeeded(allowance.Id);
    }
}

public sealed record AllowanceAllocationRequest(int AccountId, decimal Amount);
public sealed record AllowanceOperationResult(bool IsSuccess, int? AllowanceId, string? Error)
{
    public static AllowanceOperationResult Succeeded(int id) => new(true, id, null);
    public static AllowanceOperationResult Failed(string error) => new(false, null, error);
}

public sealed record WeeklyAllowanceProgress(
    WeeklyAllowance Allowance,
    decimal AmountSpent,
    IReadOnlyList<Transaction> RecentExpenses)
{
    public decimal Remaining => Allowance.AmountReceived - AmountSpent;
}
