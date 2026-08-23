namespace Kwenta.Data;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public CategoryType Type { get; set; }

    public bool IsDefault { get; set; }

    public string? UserId { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = [];
}
