namespace Kwenta.Data;

public class Reimbursement
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ReceivingAccountId { get; set; }
    public FinancialAccount ReceivingAccount { get; set; } = null!;
    public int? ExpenseTransactionId { get; set; }
    public Transaction? ExpenseTransaction { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
}
