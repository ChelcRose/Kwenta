namespace Kwenta.Data;

public class FinancialAccount
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string AccountType { get; set; } = string.Empty;

    public string? Provider { get; set; }

    public decimal StartingBalance { get; set; }

    public bool IsArchived { get; set; }
}