namespace Kwenta.Data;

public class SavingsGoal
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal TargetAmount { get; set; }

    public decimal CurrentSavedAmount { get; set; }

    public DateOnly? TargetDate { get; set; }
}
