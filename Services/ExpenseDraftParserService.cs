using System.Globalization;
using System.Text.RegularExpressions;
using Kwenta.Data;
using Microsoft.EntityFrameworkCore;

namespace Kwenta.Services;

public sealed partial class ExpenseDraftParserService(ApplicationDbContext db, IFinancialChatAiService aiService)
{
    private const string NumberPattern = @"(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?";
    private const string PhpAmountPattern = @"(?:(?:₱|PHP\s*)" + NumberPattern + @"|" + NumberPattern + @"(?:\s*(?:PHP|pesos?))?)";

    private static readonly IReadOnlyDictionary<string, string[]> CategoryKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Food"] = ["milk tea", "coffee", "groceries", "grocery", "restaurant", "meal", "lunch", "food"],
            ["Transportation"] = ["bus", "jeep", "jeepney", "fare", "taxi", "grab", "ride", "fuel", "gas"],
            ["Shopping"] = ["shoes", "clothes", "clothing", "shirt", "bag"],
            ["Bills"] = ["electricity", "water bill", "internet", "rent", "phone bill"]
        };

    [GeneratedRegex(@"\b(?:spent|paid|bought|purchased|got)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExpenseVerbPattern();

    [GeneratedRegex(@"\b(?:was|were|cost)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CostLanguagePattern();

    [GeneratedRegex(@"(?<![\p{L}\p{N}.,])" + PhpAmountPattern + @"(?![\p{L}\p{N}.,])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountCandidatePattern();

    [GeneratedRegex(@"\b(?:using|with|from)\s+(?<account>.+?)(?=\s+(?:after|before|today|earlier|this\s+(?:morning|afternoon|evening))\b|[?.!,]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AccountReferencePattern();

    [GeneratedRegex(@"^(?:i\s+|we\s+)?(?:spent|paid)\s+(?:my\s+)?" + PhpAmountPattern + @"\s+(?:on|for)\s+(?<merchant>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountFirstMerchantPattern();

    [GeneratedRegex(@"^(?:i\s+|we\s+)?(?:bought|purchased|got)\s+(?<merchant>.+?)\s+for\s+" + PhpAmountPattern + @"(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MerchantFirstPattern();

    [GeneratedRegex(@"^(?:i\s+|we\s+)?(?:bought|purchased|got)\s+" + PhpAmountPattern + @"\s+(?<merchant>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BoughtAmountFirstPattern();

    public static bool LooksLikeExpenseEntry(string message)
    {
        var input = message.Trim();
        if (input.Length == 0 || PurchaseQuestionParser.LooksLikePurchaseQuestion(input) ||
            PurchaseQuestionParser.LooksLikePurchasePlanQuestion(input))
        {
            return false;
        }

        return input.Any(char.IsDigit) &&
               (ExpenseVerbPattern().IsMatch(input) || CostLanguagePattern().IsMatch(input));
    }

    public async Task<ExpenseDraftParseResult> ParseAsync(
        string userId, string originalMessage, DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var message = originalMessage.Trim();
        if (!LooksLikeExpenseEntry(message))
        {
            return ExpenseDraftParseResult.Failed(
                "I couldn't confidently identify an expense-entry request. Try: I bought 120 pesos coffee today using cash.");
        }

        var accounts = await db.FinancialAccounts.AsNoTracking()
            .Where(account => account.UserId == userId && !account.IsArchived)
            .Select(account => new AccountCandidate(account.Id, account.Name, account.Provider))
            .ToListAsync(cancellationToken);
        var categories = await db.Categories.AsNoTracking()
            .Where(category => category.Type == CategoryType.Expense &&
                ((category.IsDefault && category.UserId == null) ||
                 (!category.IsDefault && category.UserId == userId)))
            .Select(category => new CategoryCandidate(category.Id, category.Name, category.IsDefault))
            .ToListAsync(cancellationToken);

        var warnings = new List<string>();
        var amount = ExtractAuthoritativeAmount(message, warnings);
        var merchant = ExtractDeterministicMerchant(message);
        var accountMatch = MatchAccount(ExtractAccountReference(message), accounts);
        var categoryMatch = MatchCategory(merchant, message, categories);
        var deterministicComplete = amount is not null && merchant.Length > 0 &&
                                    accountMatch.Account is not null && categoryMatch is not null;
        var categorySuggestedByAi = false;

        if (!deterministicComplete)
        {
            var aiResult = await aiService.ProposeExpenseDraftAsync(
                message,
                today,
                accounts.Select(account => new ExpenseAccountAiCandidate(account.Name, account.Provider)).ToList(),
                categories.Select(category => category.Name).Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name).ToList(),
                cancellationToken);
            var proposal = aiResult.Proposal;

            if (proposal is not null && proposal.Intent == "expense" &&
                proposal.Confidence is "high" or "medium")
            {
                ValidateProposedAmount(proposal.AmountText, amount, warnings);

                if (merchant.Length == 0 && IsSupportedText(proposal.Merchant, message))
                {
                    merchant = CleanMerchant(proposal.Merchant!);
                }

                if (accountMatch.Account is null && IsSupportedText(proposal.AccountReference, message))
                {
                    accountMatch = MatchAccount(proposal.AccountReference, accounts);
                }

                if (categoryMatch is null && !string.IsNullOrWhiteSpace(proposal.CategorySuggestion))
                {
                    categoryMatch = categories
                        .Where(category => string.Equals(category.Name, proposal.CategorySuggestion,
                            StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(category => category.IsDefault).ThenBy(category => category.Id)
                        .FirstOrDefault();
                    categorySuggestedByAi = categoryMatch is not null;
                }

                ValidateSuggestedDate(proposal.DateText, today, message, warnings);
                warnings.Add("AI helped interpret this draft. Review every field before confirming.");
            }
            else
            {
                warnings.Add(aiResult.IsConfigured
                    ? "AI interpretation was unavailable or uncertain; deterministic fields are shown for review."
                    : "AI is not configured; deterministic fields are shown for review.");
            }
        }

        if (accountMatch.Warning is not null) warnings.Add(accountMatch.Warning);
        if (categoryMatch is null)
            warnings.Add("No confident existing Expense category suggestion was found.");
        else if (categorySuggestedByAi)
            warnings.Add("Category was suggested from your valid Expense categories.");

        var missingFields = new List<string>();
        if (amount is null) missingFields.Add("Amount");
        if (merchant.Length == 0) missingFields.Add("Merchant/payee");
        if (accountMatch.Account is null) missingFields.Add("Account");
        if (categoryMatch is null) missingFields.Add("Category");

        return ExpenseDraftParseResult.Succeeded(new ParsedExpenseDraft(
            amount, TransactionType.Expense, merchant, message, today,
            accountMatch.Account?.Id, accountMatch.Account?.Name,
            categoryMatch?.Id, categoryMatch?.Name, missingFields, warnings));
    }

    private static decimal? ExtractAuthoritativeAmount(string message, List<string> warnings)
    {
        var candidates = AmountCandidatePattern().Matches(message);
        if (candidates.Count != 1)
        {
            warnings.Add(candidates.Count == 0
                ? "No single valid PHP amount was found; enter the amount manually."
                : "More than one possible amount was found; enter the authoritative amount manually.");
            return null;
        }

        var normalized = Regex.Replace(candidates[0].Value, @"₱|PHP|pesos?|,|\s", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out var amount) || amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            warnings.Add("The proposed amount is invalid; enter a positive PHP amount with at most two decimal places.");
            return null;
        }

        return amount;
    }

    private static void ValidateProposedAmount(
        string? amountText,
        decimal? authoritativeAmount,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(amountText))
        {
            return;
        }

        var proposalWarnings = new List<string>();
        var proposedAmount = ExtractAuthoritativeAmount(amountText, proposalWarnings);
        if (proposedAmount is null ||
            authoritativeAmount is null ||
            proposedAmount != authoritativeAmount)
        {
            warnings.Add("The AI-proposed amount was not authoritative; only the amount validated from your original message was used.");
        }
    }

    private static string ExtractDeterministicMerchant(string message)
    {
        foreach (var pattern in new[] { AmountFirstMerchantPattern(), MerchantFirstPattern(), BoughtAmountFirstPattern() })
        {
            var match = pattern.Match(message);
            if (match.Success) return CleanMerchant(match.Groups["merchant"].Value);
        }
        return string.Empty;
    }

    private static string CleanMerchant(string value)
    {
        var merchant = value.Trim().TrimEnd('?', '.', '!', ',').Trim();
        var markers = new[] { " using ", " with ", " from ", " after ", " before ", " today", " earlier",
            " this morning", " this afternoon", " this evening" };
        var cutAt = markers.Select(marker => merchant.IndexOf(marker, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0).DefaultIfEmpty(merchant.Length).Min();
        return merchant[..cutAt].Trim().TrimEnd(',', '.', '?', '!').Trim();
    }

    private static string? ExtractAccountReference(string message)
    {
        var match = AccountReferencePattern().Match(message);
        return match.Success ? match.Groups["account"].Value.Trim() : null;
    }

    private static bool IsSupportedText(string? suggestion, string message) =>
        !string.IsNullOrWhiteSpace(suggestion) && ContainsPhrase(message, suggestion);

    private static void ValidateSuggestedDate(string? dateText, DateOnly today, string message, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(dateText) || string.Equals(dateText, "today", StringComparison.OrdinalIgnoreCase))
            return;

        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedDate) || parsedDate != today ||
            !message.Contains(dateText, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("The suggested date was not a supported explicit date, so the draft uses today. Review it before confirming.");
        }
    }

    private static AccountMatch MatchAccount(string? accountReference, IReadOnlyList<AccountCandidate> accounts)
    {
        if (string.IsNullOrWhiteSpace(accountReference))
            return new(null, "No active owned account was confidently identified.");

        var matches = accounts.Where(account => IsAccountTextMatch(accountReference, account.Name) ||
                (!string.IsNullOrWhiteSpace(account.Provider) && IsAccountTextMatch(accountReference, account.Provider)))
            .GroupBy(account => account.Id).Select(group => group.First()).ToList();
        return matches.Count switch
        {
            1 => new(matches[0]),
            > 1 => new(null, "More than one active owned account matches the message."),
            _ => new(null, "The account reference does not match one active account you own.")
        };
    }

    private static bool IsAccountTextMatch(string reference, string candidate) =>
        Normalize(reference) == Normalize(candidate) || ContainsPhrase(reference, candidate) || ContainsPhrase(candidate, reference);

    private static CategoryCandidate? MatchCategory(string merchant, string message,
        IReadOnlyList<CategoryCandidate> categories)
    {
        var text = merchant.Length > 0 ? merchant : message;
        var exact = categories.Where(category => Normalize(category.Name) == Normalize(text) ||
                ContainsPhrase(text, category.Name))
            .OrderByDescending(category => category.IsDefault).ThenBy(category => category.Id).FirstOrDefault();
        if (exact is not null) return exact;

        var mappedName = CategoryKeywords.FirstOrDefault(mapping =>
            mapping.Value.Any(keyword => ContainsPhrase(text, keyword))).Key;
        return mappedName is null ? null : categories.Where(category => category.IsDefault &&
                string.Equals(category.Name, mappedName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(category => category.Id).FirstOrDefault();
    }

    private static bool ContainsPhrase(string text, string phrase) => !string.IsNullOrWhiteSpace(phrase) &&
        Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(phrase.Trim())}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string Normalize(string value) => string.Join(' ', value.Split(' ',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();

    private sealed record AccountCandidate(int Id, string Name, string? Provider);
    private sealed record AccountMatch(AccountCandidate? Account, string? Warning = null);
    private sealed record CategoryCandidate(int Id, string Name, bool IsDefault);
}

public sealed record ParsedExpenseDraft(decimal? Amount, TransactionType Type, string Merchant,
    string Description, DateOnly Date, int? SuggestedAccountId, string? SuggestedAccountName,
    int? SuggestedCategoryId, string? SuggestedCategoryName, IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> ParseWarnings);

public sealed record ExpenseDraftParseResult(bool IsSuccess, ParsedExpenseDraft? Draft, string? ErrorMessage)
{
    public static ExpenseDraftParseResult Succeeded(ParsedExpenseDraft draft) => new(true, draft, null);
    public static ExpenseDraftParseResult Failed(string errorMessage) => new(false, null, errorMessage);
}
