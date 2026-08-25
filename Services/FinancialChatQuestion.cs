namespace Kwenta.Services;

public enum FinancialChatQuestion
{
    TotalExpenses,
    TotalIncome,
    HighestSpendingCategory,
    LargestExpense
}

public static class FinancialChatQuestionRouter
{
    public static bool TryRoute(string question, out FinancialChatQuestion routedQuestion)
    {
        var normalized = question.Trim().TrimEnd('?', '.', '!').ToLowerInvariant();

        routedQuestion = normalized switch
        {
            "how much did i spend this month" => FinancialChatQuestion.TotalExpenses,
            "how much income did i receive this month" => FinancialChatQuestion.TotalIncome,
            "where did most of my money go this month" => FinancialChatQuestion.HighestSpendingCategory,
            "what was my biggest expense this month" => FinancialChatQuestion.LargestExpense,
            _ => default
        };

        return normalized is
            "how much did i spend this month" or
            "how much income did i receive this month" or
            "where did most of my money go this month" or
            "what was my biggest expense this month";
    }
}
