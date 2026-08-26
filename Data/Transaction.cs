namespace Kwenta.Data;

public class Transaction
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int AccountId { get; set; }

    public FinancialAccount FinancialAccount { get; set; } = null!;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Date { get; set; }

    public string Merchant { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? WeeklyAllowanceId { get; set; }

    public WeeklyAllowance? WeeklyAllowance { get; set; }
}
