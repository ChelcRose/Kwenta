namespace Kwenta.Data;

public class WeeklyAllowance
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal AmountReceived { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ClosedAt { get; set; }
    public ICollection<WeeklyAllowanceAllocation> Allocations { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}
