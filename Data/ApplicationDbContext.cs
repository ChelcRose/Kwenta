using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<FinancialAccount> FinancialAccounts { get; set; }

    public DbSet<Category> Categories { get; set; }

    public DbSet<Transaction> Transactions { get; set; }

    public DbSet<Budget> Budgets { get; set; }

    public DbSet<SavingsGoal> SavingsGoals { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Transaction>()
            .HasOne(transaction => transaction.FinancialAccount)
            .WithMany(account => account.Transactions)
            .HasForeignKey(transaction => transaction.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Transaction>()
            .HasOne(transaction => transaction.Category)
            .WithMany(category => category.Transactions)
            .HasForeignKey(transaction => transaction.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Category>()
            .HasIndex(category => new { category.UserId, category.Name, category.Type })
            .IsUnique();

        builder.Entity<Budget>()
            .HasOne(budget => budget.Category)
            .WithMany(category => category.Budgets)
            .HasForeignKey(budget => budget.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Budget>()
            .HasIndex(budget => new { budget.UserId, budget.CategoryId, budget.Month, budget.Year })
            .IsUnique();

        builder.Entity<SavingsGoal>()
            .HasIndex(goal => goal.UserId);

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
