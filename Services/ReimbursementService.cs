using System.Data;
using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed class ReimbursementService(ApplicationDbContext db)
{
    public async Task<ReimbursementOperationResult> CreateAsync(
        string userId,
        int receivingAccountId,
        decimal amount,
        DateOnly date,
        string? description,
        int? expenseTransactionId,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            return ReimbursementOperationResult.Failed("Amount must be greater than zero with at most two decimal places.");

        await using var databaseTransaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var account = await db.FinancialAccounts.SingleOrDefaultAsync(account =>
            account.Id == receivingAccountId && account.UserId == userId && !account.IsArchived,
            cancellationToken);
        if (account is null)
            return ReimbursementOperationResult.Failed("Choose an active receiving account you own.");

        Transaction? expense = null;
        if (expenseTransactionId is not null)
        {
            expense = await db.Transactions.SingleOrDefaultAsync(transaction =>
                transaction.Id == expenseTransactionId &&
                transaction.UserId == userId &&
                transaction.FinancialAccount.UserId == userId &&
                transaction.Type == TransactionType.Expense,
                cancellationToken);
            if (expense is null)
                return ReimbursementOperationResult.Failed("Choose an expense you own.");

            var reimbursed = await db.Reimbursements
                .Where(item => item.UserId == userId && item.ExpenseTransactionId == expense.Id)
                .SumAsync(item => item.Amount, cancellationToken);
            if (amount > expense.Amount - reimbursed)
                return ReimbursementOperationResult.Failed(
                    $"Only {FormatPeso(expense.Amount - reimbursed)} remains reimbursable for that expense.");
        }

        var reimbursement = new Reimbursement
        {
            UserId = userId,
            ReceivingAccountId = receivingAccountId,
            ExpenseTransactionId = expense?.Id,
            Amount = amount,
            Date = date,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        };
        db.Reimbursements.Add(reimbursement);
        await db.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);

        return ReimbursementOperationResult.Succeeded(reimbursement.Id, account.Name);
    }

    private static string FormatPeso(decimal amount) => $"₱{amount:N2}";
}

public sealed record ReimbursementOperationResult(
    bool IsSuccess,
    int? ReimbursementId,
    string? ReceivingAccountName,
    string? Error)
{
    public static ReimbursementOperationResult Failed(string error) => new(false, null, null, error);
    public static ReimbursementOperationResult Succeeded(int id, string accountName) =>
        new(true, id, accountName, null);
}
