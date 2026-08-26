namespace Kwenta.Services;

public interface IFinancialChatAiService
{
    Task<FinancialChatAiResult> ExplainAsync(
        FinancialChatQuestion question,
        string userQuestion,
        string financialContext,
        CancellationToken cancellationToken = default);

    Task<ExpenseCategorySuggestionResult> SuggestExpenseCategoryAsync(
        string merchant,
        string description,
        IReadOnlyList<string> validCategoryNames,
        CancellationToken cancellationToken = default);
}

public sealed record ExpenseCategorySuggestionResult(
    bool IsConfigured,
    string? CategoryName)
{
    public static ExpenseCategorySuggestionResult Suggestion(string categoryName) =>
        new(true, categoryName);

    public static ExpenseCategorySuggestionResult NoSuggestion(bool isConfigured = true) =>
        new(isConfigured, null);
}

public sealed record FinancialChatAiResult(bool IsSuccess, string? Response, bool IsConfigured)
{
    public static FinancialChatAiResult Success(string response) => new(true, response, true);

    public static FinancialChatAiResult NotConfigured() => new(false, null, false);

    public static FinancialChatAiResult Failed() => new(false, null, true);
}
