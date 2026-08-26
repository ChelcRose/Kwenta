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

    Task<ExpenseDraftAiResult> ProposeExpenseDraftAsync(
        string originalMessage,
        DateOnly currentDate,
        IReadOnlyList<ExpenseAccountAiCandidate> activeAccounts,
        IReadOnlyList<string> validExpenseCategoryNames,
        CancellationToken cancellationToken = default);
}

public sealed record ExpenseAccountAiCandidate(string Name, string? Provider);

public sealed record ExpenseDraftAiProposal(
    string Intent,
    string? AmountText,
    string? Merchant,
    string? AccountReference,
    string? CategorySuggestion,
    string? DateText,
    string? Description,
    string Confidence);

public sealed record ExpenseDraftAiResult(
    bool IsConfigured,
    ExpenseDraftAiProposal? Proposal)
{
    public static ExpenseDraftAiResult Proposed(ExpenseDraftAiProposal proposal) =>
        new(true, proposal);

    public static ExpenseDraftAiResult NoProposal(bool isConfigured = true) =>
        new(isConfigured, null);
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
