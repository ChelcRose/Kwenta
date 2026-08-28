using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed class TransferService(ApplicationDbContext db)
{
    public async Task<TransferOperationResult> CreateAsync(
        string userId,
        int fromAccountId,
        int toAccountId,
        decimal amount,
        DateOnly date,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (fromAccountId == toAccountId)
            return TransferOperationResult.Failed("Choose two different accounts.");
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            return TransferOperationResult.Failed("Amount must be greater than zero with at most two decimal places.");

        var accounts = await db.FinancialAccounts.AsNoTracking()
            .Where(account => (account.Id == fromAccountId || account.Id == toAccountId) &&
                account.UserId == userId && !account.IsArchived)
            .ToListAsync(cancellationToken);
        var fromAccount = accounts.SingleOrDefault(account => account.Id == fromAccountId);
        var toAccount = accounts.SingleOrDefault(account => account.Id == toAccountId);
        if (fromAccount is null || toAccount is null)
            return TransferOperationResult.Failed("Both accounts must be active accounts you own.");

        var balances = await GetBalancesAsync(userId, cancellationToken: cancellationToken);
        var fromBalance = balances.GetValueOrDefault(fromAccountId);
        if (amount > fromBalance)
            return TransferOperationResult.Failed(
                $"Insufficient balance. {fromAccount.Name} currently has {FormatPeso(fromBalance)}.");

        var transfer = new Transfer
        {
            UserId = userId,
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = amount,
            Date = date,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        };
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken);

        var totalBefore = balances.Values.Sum();
        return TransferOperationResult.Succeeded(
            transfer.Id,
            fromAccount.Name,
            toAccount.Name,
            fromBalance - amount,
            balances.GetValueOrDefault(toAccountId) + amount,
            totalBefore);
    }

    public async Task<Dictionary<int, decimal>> GetBalancesAsync(
        string userId,
        IReadOnlyCollection<int>? accountIds = null,
        CancellationToken cancellationToken = default)
    {
        var accountsQuery = db.FinancialAccounts.AsNoTracking()
            .Where(account => account.UserId == userId && !account.IsArchived);
        if (accountIds is not null)
            accountsQuery = accountsQuery.Where(account => accountIds.Contains(account.Id));
        var accounts = await accountsQuery.Select(account => new { account.Id, account.StartingBalance })
            .ToListAsync(cancellationToken);
        var ids = accounts.Select(account => account.Id).ToList();

        var transactions = await db.Transactions.AsNoTracking()
            .Where(transaction => transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId && ids.Contains(transaction.AccountId))
            .Select(transaction => new { transaction.AccountId, transaction.Type, transaction.Amount })
            .ToListAsync(cancellationToken);
        var transfers = await db.Transfers.AsNoTracking()
            .Where(transfer => transfer.UserId == userId &&
                transfer.FromAccount.UserId == userId && transfer.ToAccount.UserId == userId &&
                (ids.Contains(transfer.FromAccountId) || ids.Contains(transfer.ToAccountId)))
            .Select(transfer => new { transfer.FromAccountId, transfer.ToAccountId, transfer.Amount })
            .ToListAsync(cancellationToken);
        var reimbursements = await db.Reimbursements.AsNoTracking()
            .Where(item => item.UserId == userId && item.ReceivingAccount.UserId == userId &&
                ids.Contains(item.ReceivingAccountId))
            .Select(item => new { item.ReceivingAccountId, item.Amount })
            .ToListAsync(cancellationToken);

        return accounts.ToDictionary(account => account.Id, account =>
            FinancialCalculations.AccountBalance(
                account.StartingBalance,
                transactions.Where(item => item.AccountId == account.Id && item.Type == TransactionType.Income).Sum(item => item.Amount),
                transactions.Where(item => item.AccountId == account.Id && item.Type == TransactionType.Expense).Sum(item => item.Amount),
                transfers.Where(item => item.ToAccountId == account.Id).Sum(item => item.Amount),
                transfers.Where(item => item.FromAccountId == account.Id).Sum(item => item.Amount),
                reimbursements.Where(item => item.ReceivingAccountId == account.Id).Sum(item => item.Amount)));
    }

    private static string FormatPeso(decimal amount) => $"₱{amount:N2}";
}

public sealed record TransferOperationResult(
    bool IsSuccess,
    int? TransferId,
    string? Error,
    string? FromAccountName,
    string? ToAccountName,
    decimal? FromBalance,
    decimal? ToBalance,
    decimal? TotalBalance)
{
    public static TransferOperationResult Failed(string error) =>
        new(false, null, error, null, null, null, null, null);

    public static TransferOperationResult Succeeded(int id, string fromName, string toName,
        decimal fromBalance, decimal toBalance, decimal totalBalance) =>
        new(true, id, null, fromName, toName, fromBalance, toBalance, totalBalance);
}
