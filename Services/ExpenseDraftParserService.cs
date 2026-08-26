using System.Globalization;
using System.Text.RegularExpressions;
using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed partial class ExpenseDraftParserService(
    ApplicationDbContext db,
    IFinancialChatAiService aiService)
{
    private const string AmountPattern = @"(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?";

    private static readonly IReadOnlyDictionary<string, string[]> CategoryKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Food"] = ["milk tea", "coffee", "groceries", "grocery", "restaurant", "meal", "food"],
            ["Transportation"] = ["bus", "jeep", "jeepney", "fare", "taxi", "grab", "fuel", "gas"],
            ["Shopping"] = ["shoes", "clothes", "clothing", "shirt", "bag"],
            ["Bills"] = ["electricity", "water bill", "internet", "rent", "phone bill"]
        };

    [GeneratedRegex(@"^(?:spent|paid|bought|purchased)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExpenseIntentPattern();

    [GeneratedRegex(
        @"^(?:spent|paid)\s+(?:₱|php\s*)(?<amount>" + AmountPattern + @")\s+(?:on|for)\s+(?<merchant>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountFirstExpensePattern();

    [GeneratedRegex(
        @"^(?:bought|purchased)\s+(?<merchant>.+?)\s+for\s+(?:₱|php\s*)(?<amount>" + AmountPattern + @")\s*[?.!]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MerchantFirstExpensePattern();

    [GeneratedRegex(@"(?:₱|php\s*)[^\s]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyCandidatePattern();

    [GeneratedRegex(
        @"\busing\s+(?<account>.+?)(?=\s+(?:after|before)\b|[?.!]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AccountReferencePattern();

    public static bool LooksLikeExpenseEntry(string message) =>
        ExpenseIntentPattern().IsMatch(message.Trim());

    public async Task<ExpenseDraftParseResult> ParseAsync(
        string userId,
        string originalMessage,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var message = originalMessage.Trim();
        if (!LooksLikeExpenseEntry(message))
        {
            return ExpenseDraftParseResult.Failed(
                "Use a clear expense phrase such as: Spent ₱180 on milk tea using GCash.");
        }

        var currencyCandidates = CurrencyCandidatePattern().Matches(message);
        if (currencyCandidates.Count == 0)
        {
            return ExpenseDraftParseResult.Failed(
                "Include one PHP amount, such as ₱180 or PHP 180.");
        }

        if (currencyCandidates.Count > 1)
        {
            return ExpenseDraftParseResult.Failed(
                "I found more than one PHP amount. Use one unambiguous expense amount.");
        }

        var match = AmountFirstExpensePattern().Match(message);
        if (!match.Success)
        {
            match = MerchantFirstExpensePattern().Match(message);
        }

        if (!match.Success || !TryParseAmount(match.Groups["amount"].Value, out var amount))
        {
            return ExpenseDraftParseResult.Failed(
                "The expense amount or sentence format is invalid. Use a format such as: Paid ₱450 for groceries using Maya.");
        }

        var merchant = ExtractMerchant(match.Groups["merchant"].Value);
        var accounts = await db.FinancialAccounts
            .AsNoTracking()
            .Where(account => account.UserId == userId && !account.IsArchived)
            .Select(account => new AccountCandidate(
                account.Id,
                account.Name,
                account.Provider))
            .ToListAsync(cancellationToken);

        var accountReferenceMatch = AccountReferencePattern().Match(message);
        var accountReference = accountReferenceMatch.Success
            ? accountReferenceMatch.Groups["account"].Value.Trim()
            : null;
        var accountMatch = MatchAccount(accountReference, accounts);

        var categories = await db.Categories
            .AsNoTracking()
            .Where(category =>
                category.Type == CategoryType.Expense &&
                ((category.IsDefault && category.UserId == null) ||
                 (!category.IsDefault && category.UserId == userId)))
            .Select(category => new CategoryCandidate(
                category.Id,
                category.Name,
                category.IsDefault))
            .ToListAsync(cancellationToken);

        var categoryMatch = MatchCategory(merchant, categories);
        var categorySuggestedByAi = false;

        if (categoryMatch is null && categories.Count > 0)
        {
            var validCategoryNames = categories
                .Select(category => category.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();
            var aiSuggestion = await aiService.SuggestExpenseCategoryAsync(
                merchant,
                message,
                validCategoryNames,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(aiSuggestion.CategoryName))
            {
                var candidate = categories
                    .Where(category => string.Equals(
                        category.Name,
                        aiSuggestion.CategoryName,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(category => category.IsDefault)
                    .ThenBy(category => category.Id)
                    .FirstOrDefault();

                if (candidate is not null)
                {
                    categoryMatch = await db.Categories
                        .AsNoTracking()
                        .Where(category =>
                            category.Id == candidate.Id &&
                            category.Type == CategoryType.Expense &&
                            ((category.IsDefault && category.UserId == null) ||
                             (!category.IsDefault && category.UserId == userId)))
                        .Select(category => new CategoryCandidate(
                            category.Id,
                            category.Name,
                            category.IsDefault))
                        .SingleOrDefaultAsync(cancellationToken);
                    categorySuggestedByAi = categoryMatch is not null;
                }
            }
        }
        var missingFields = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(merchant))
        {
            missingFields.Add("Merchant/payee");
        }

        if (accountMatch.Account is null)
        {
            missingFields.Add("Account");
        }

        if (categoryMatch is null)
        {
            missingFields.Add("Category");
        }

        if (accountMatch.Warning is not null)
        {
            warnings.Add(accountMatch.Warning);
        }

        if (categoryMatch is null)
        {
            warnings.Add("No confident existing Expense category suggestion was found.");
        }
        else if (categorySuggestedByAi)
        {
            warnings.Add("Category was suggested by AI from your valid Expense categories. Review it before confirming.");
        }

        var draft = new ParsedExpenseDraft(
            amount,
            TransactionType.Expense,
            merchant,
            message,
            today,
            accountMatch.Account?.Id,
            accountMatch.Account?.Name,
            categoryMatch?.Id,
            categoryMatch?.Name,
            missingFields,
            warnings);

        return ExpenseDraftParseResult.Succeeded(draft);
    }

    private static bool TryParseAmount(string amountText, out decimal amount) =>
        decimal.TryParse(
            amountText,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out amount) &&
        amount > 0;

    private static string ExtractMerchant(string rawMerchant)
    {
        var merchant = rawMerchant.Trim().TrimEnd('?', '.', '!').Trim();
        var contextMarkers = new[] { " using ", " after ", " before " };
        var cutAt = contextMarkers
            .Select(marker => merchant.IndexOf(marker, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(merchant.Length)
            .Min();

        return merchant[..cutAt].Trim().TrimEnd(',', '.', '?', '!').Trim();
    }

    private static AccountMatch MatchAccount(
        string? accountReference,
        IReadOnlyList<AccountCandidate> accounts)
    {
        if (string.IsNullOrWhiteSpace(accountReference))
        {
            return new(null, "No account was explicitly identified with a using phrase.");
        }

        var matches = accounts
            .Where(account =>
                ContainsPhrase(accountReference, account.Name) ||
                (!string.IsNullOrWhiteSpace(account.Provider) &&
                 ContainsPhrase(accountReference, account.Provider)))
            .GroupBy(account => account.Id)
            .Select(group => group.First())
            .ToList();

        if (matches.Count == 1)
        {
            return new(matches[0]);
        }

        if (matches.Count > 1)
        {
            return new(null, "More than one active owned account matches the message.");
        }

        var mentionsCommonProvider = ContainsPhrase(accountReference, "GCash") ||
                                     ContainsPhrase(accountReference, "Maya");
        return new(
            null,
            mentionsCommonProvider
                ? "The mentioned provider does not uniquely match an active account you own."
                : "No account was explicitly matched from the message.");
    }

    private static CategoryCandidate? MatchCategory(
        string merchant,
        IReadOnlyList<CategoryCandidate> categories)
    {
        if (merchant.Length == 0)
        {
            return null;
        }

        var exactMatches = categories
            .Where(category =>
                Normalize(category.Name) == Normalize(merchant) ||
                ContainsPhrase(merchant, category.Name))
            .OrderByDescending(category => category.IsDefault)
            .ToList();

        if (exactMatches.Count > 0)
        {
            return exactMatches[0];
        }

        var mappedCategoryName = CategoryKeywords
            .FirstOrDefault(mapping =>
                mapping.Value.Any(keyword => ContainsPhrase(merchant, keyword)))
            .Key;

        if (mappedCategoryName is null)
        {
            return null;
        }

        return categories
            .Where(category =>
                category.IsDefault &&
                string.Equals(category.Name, mappedCategoryName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(category => category.Id)
            .FirstOrDefault();
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        return Regex.IsMatch(
            text,
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(phrase.Trim())}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToUpperInvariant();

    private sealed record AccountCandidate(int Id, string Name, string? Provider);

    private sealed record AccountMatch(AccountCandidate? Account, string? Warning = null);

    private sealed record CategoryCandidate(int Id, string Name, bool IsDefault);
}

public sealed record ParsedExpenseDraft(
    decimal Amount,
    TransactionType Type,
    string Merchant,
    string Description,
    DateOnly Date,
    int? SuggestedAccountId,
    string? SuggestedAccountName,
    int? SuggestedCategoryId,
    string? SuggestedCategoryName,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> ParseWarnings);

public sealed record ExpenseDraftParseResult(
    bool IsSuccess,
    ParsedExpenseDraft? Draft,
    string? ErrorMessage)
{
    public static ExpenseDraftParseResult Succeeded(ParsedExpenseDraft draft) =>
        new(true, draft, null);

    public static ExpenseDraftParseResult Failed(string errorMessage) =>
        new(false, null, errorMessage);
}
