using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<FinancialAccount> FinancialAccounts { get; set; }

    public DbSet<Category> Categories { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Food", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 2, Name = "Transportation", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 3, Name = "Wants/Personal", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 4, Name = "Bills", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 5, Name = "Shopping", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 6, Name = "Savings", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 7, Name = "Salary", Type = CategoryType.Income, IsDefault = true },
            new Category { Id = 8, Name = "Gift", Type = CategoryType.Income, IsDefault = true });
    }
}
