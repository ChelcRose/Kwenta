namespace Kwenta.Services;

public interface IFinancialChatAiService
{
    Task<FinancialChatAiResult> ExplainAsync(
        FinancialChatQuestion question,
        string userQuestion,
        string financialContext,
        CancellationToken cancellationToken = default);
}

public sealed record FinancialChatAiResult(bool IsSuccess, string? Response, bool IsConfigured)
{
    public static FinancialChatAiResult Success(string response) => new(true, response, true);

    public static FinancialChatAiResult NotConfigured() => new(false, null, false);

    public static FinancialChatAiResult Failed() => new(false, null, true);
}
