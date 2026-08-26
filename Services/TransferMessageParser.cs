using System.Globalization;
using System.Text.RegularExpressions;

namespace Kwenta.Services;

public static partial class TransferMessageParser
{
    private const string AmountPattern = @"(?:(?:₱|PHP\s*)?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?(?:\s*pesos?)?)";

    [GeneratedRegex(@"^\s*(?:move|transfer)\s+(?<amount>" + AmountPattern + @")\s+from\s+(?<from>.+?)\s+to\s+(?<to>.+?)\s*[?.!]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    [GeneratedRegex(@"^\s*(?:move|transfer)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IntentPattern();

    public static bool LooksLikeTransfer(string message) => IntentPattern().IsMatch(message);

    public static TransferMessageDraft Parse(string message, IReadOnlyList<TransferAccountCandidate> accounts)
    {
        var match = Pattern().Match(message);
        if (!match.Success) return new(null, null, null, "Use: Transfer ₱500 from GCash to Maya.");
        var amount = ParseAmount(match.Groups["amount"].Value);
        var from = Resolve(match.Groups["from"].Value.Trim(), accounts);
        var to = Resolve(match.Groups["to"].Value.Trim().TrimEnd('.', '!', '?'), accounts);
        return new(amount, from, to,
            amount is null ? "Enter a valid positive PHP amount with at most two decimal places." : null);
    }

    private static int? Resolve(string reference, IReadOnlyList<TransferAccountCandidate> accounts)
    {
        var matches = accounts.Where(account => Matches(reference, account.Name) ||
                (!string.IsNullOrWhiteSpace(account.Provider) && Matches(reference, account.Provider)))
            .GroupBy(account => account.Id).Select(group => group.First()).ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }

    private static bool Matches(string reference, string candidate) =>
        string.Equals(reference, candidate, StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(reference, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(candidate)}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static decimal? ParseAmount(string text)
    {
        var normalized = Regex.Replace(text, @"₱|PHP|pesos?|,|\s", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                   out var amount) && amount > 0 && decimal.Round(amount, 2) == amount
            ? amount : null;
    }
}

public sealed record TransferAccountCandidate(int Id, string Name, string? Provider);
public sealed record TransferMessageDraft(decimal? Amount, int? FromAccountId, int? ToAccountId, string? Warning);
