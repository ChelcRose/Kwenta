namespace Kwenta.Data;

public class Transfer
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int FromAccountId { get; set; }
    public FinancialAccount FromAccount { get; set; } = null!;
    public int ToAccountId { get; set; }
    public FinancialAccount ToAccount { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
}
