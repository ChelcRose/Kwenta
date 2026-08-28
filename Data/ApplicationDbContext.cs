using Microsoft.AspNetCore.Identity;
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

    public DbSet<WeeklyAllowance> WeeklyAllowances { get; set; }

    public DbSet<WeeklyAllowanceAllocation> WeeklyAllowanceAllocations { get; set; }

    public DbSet<Transfer> Transfers { get; set; }

    public DbSet<ChatConversation> ChatConversations { get; set; }

    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Keep EF CLI design-time modeling aligned with Identity schema v3 used at runtime.
        builder.Entity<IdentityUserPasskey<string>>(passkey =>
        {
            passkey.ToTable("AspNetUserPasskeys");
            passkey.HasKey(item => item.CredentialId);
            passkey.Property(item => item.CredentialId).HasMaxLength(1024);
            passkey.OwnsOne(item => item.Data, data => data.ToJson("Data"));
            passkey.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            passkey.HasIndex(item => item.UserId);
        });

        builder.Entity<Transaction>()
            .HasOne(transaction => transaction.FinancialAccount)
            .WithMany(account => account.Transactions)
            .HasForeignKey(transaction => transaction.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatConversation>(conversation =>
        {
            conversation.Property(item => item.Title).HasMaxLength(60);
            conversation.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            conversation.HasIndex(item => new { item.UserId, item.UpdatedAt });
        });

        builder.Entity<ChatMessage>(message =>
        {
            message.HasOne(item => item.ChatConversation)
                .WithMany(conversation => conversation.Messages)
                .HasForeignKey(item => item.ChatConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            message.HasIndex(item => new { item.ChatConversationId, item.CreatedAt });
        });

        builder.Entity<Transfer>()
            .HasOne(transfer => transfer.FromAccount)
            .WithMany(account => account.TransfersOut)
            .HasForeignKey(transfer => transfer.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Transfer>()
            .HasOne(transfer => transfer.ToAccount)
            .WithMany(account => account.TransfersIn)
            .HasForeignKey(transfer => transfer.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Transfer>()
            .HasIndex(transfer => new { transfer.UserId, transfer.Date });

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

        builder.Entity<WeeklyAllowance>()
            .HasIndex(allowance => allowance.UserId)
            .IsUnique()
            .HasFilter("\"IsActive\" IS TRUE");

        builder.Entity<WeeklyAllowanceAllocation>()
            .HasOne(allocation => allocation.WeeklyAllowance)
            .WithMany(allowance => allowance.Allocations)
            .HasForeignKey(allocation => allocation.WeeklyAllowanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<WeeklyAllowanceAllocation>()
            .HasOne(allocation => allocation.FinancialAccount)
            .WithMany(account => account.WeeklyAllowanceAllocations)
            .HasForeignKey(allocation => allocation.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<WeeklyAllowanceAllocation>()
            .HasIndex(allocation => new { allocation.WeeklyAllowanceId, allocation.FinancialAccountId })
            .IsUnique();

        builder.Entity<Transaction>()
            .HasOne(transaction => transaction.WeeklyAllowance)
            .WithMany(allowance => allowance.Transactions)
            .HasForeignKey(transaction => transaction.WeeklyAllowanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Food", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 2, Name = "Transportation", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 3, Name = "Wants/Personal", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 4, Name = "Bills", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 5, Name = "Shopping", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 6, Name = "Savings", Type = CategoryType.Expense, IsDefault = true },
            new Category { Id = 7, Name = "Salary", Type = CategoryType.Income, IsDefault = true },
            new Category { Id = 8, Name = "Gift", Type = CategoryType.Income, IsDefault = true },
            new Category { Id = 9, Name = "Allowance", Type = CategoryType.Income, IsDefault = true });
    }
}
