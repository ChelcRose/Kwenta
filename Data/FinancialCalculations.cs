namespace Kwenta.Data;

public static class FinancialCalculations
{
    public static decimal AccountBalance(
        decimal startingBalance,
        decimal income,
        decimal expenses,
        decimal transfersIn,
        decimal transfersOut) =>
        startingBalance + income - expenses + transfersIn - transfersOut;

    public static decimal AmountSaved(decimal income, decimal expenses) =>
        income - expenses;

    public static decimal BudgetRemaining(decimal limitAmount, decimal amountSpent) =>
        limitAmount - amountSpent;

    public static decimal BudgetPercentageUsed(decimal limitAmount, decimal amountSpent) =>
        amountSpent / limitAmount * 100;

    public static bool IsOverBudget(decimal limitAmount, decimal amountSpent) =>
        amountSpent > limitAmount;

    public static decimal SavingsGoalRemaining(decimal targetAmount, decimal savedAmount) =>
        targetAmount - savedAmount;

    public static decimal SavingsGoalPercentage(decimal targetAmount, decimal savedAmount) =>
        savedAmount / targetAmount * 100;
}
