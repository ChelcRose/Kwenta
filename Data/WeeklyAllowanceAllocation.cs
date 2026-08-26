namespace Kwenta.Data;

public class WeeklyAllowanceAllocation
{
    public int Id { get; set; }
    public int WeeklyAllowanceId { get; set; }
    public WeeklyAllowance WeeklyAllowance { get; set; } = null!;
    public int FinancialAccountId { get; set; }
    public FinancialAccount FinancialAccount { get; set; } = null!;
    public decimal Amount { get; set; }
}
