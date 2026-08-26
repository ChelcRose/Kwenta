using System.Globalization;
using System.Text.RegularExpressions;

namespace Kwenta.Services;

public static partial class WeeklyAllowanceMessageParser
{
    private const string AmountPattern = @"(?:(?:₱|PHP\s*)?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?(?:\s*pesos?)?)";

    [GeneratedRegex(@"\b(?:got|received)\b.*\ballowance\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StartIntentPattern();

    [GeneratedRegex(@"^\s*(?:end|close)\s+(?:my|this)\s+week\s*[?.!]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CloseIntentPattern();

    [GeneratedRegex(@"(?:got|received)(?:\s+my)?\s+(?<amount>" + AmountPattern + @")\s+(?:weekly\s+)?allowance", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TotalPattern();

    [GeneratedRegex(@"(?<amount>" + AmountPattern + @")\s+(?:in|into|to)?\s*(?<account>[^,;]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AllocationPattern();

    public static bool LooksLikeStart(string message) => StartIntentPattern().IsMatch(message);
    public static bool LooksLikeClose(string message) => CloseIntentPattern().IsMatch(message);

    public static AllowanceMessageDraft Parse(string message, IReadOnlyList<AllowanceAccountCandidate> accounts)
    {
        var totalMatch = TotalPattern().Match(message);
        var amount = totalMatch.Success ? ParseAmount(totalMatch.Groups["amount"].Value) : null;
        var allocations = new List<AllowanceMessageAllocation>();
        var warnings = new List<string>();

        if (totalMatch.Success)
        {
            var allowanceIndex = message.IndexOf("allowance", totalMatch.Index, StringComparison.OrdinalIgnoreCase);
            var tail = allowanceIndex >= 0 ? message[(allowanceIndex + "allowance".Length)..] : string.Empty;
            foreach (Match match in AllocationPattern().Matches(tail))
            {
                var allocationAmount = ParseAmount(match.Groups["amount"].Value);
                var reference = match.Groups["account"].Value.Trim().TrimEnd('.', '!', '?').Trim();
                var matches = accounts.Where(account => Matches(reference, account.Name) ||
                        (!string.IsNullOrWhiteSpace(account.Provider) && Matches(reference, account.Provider)))
                    .GroupBy(account => account.Id).Select(group => group.First()).ToList();
                allocations.Add(new AllowanceMessageAllocation(
                    allocationAmount,
                    matches.Count == 1 ? matches[0].Id : null,
                    reference));
                if (matches.Count != 1)
                    warnings.Add($"Account reference '{reference}' was not a unique active owned account match.");
            }
        }

        return new AllowanceMessageDraft(amount, allocations, warnings);
    }

    private static decimal? ParseAmount(string text)
    {
        var normalized = Regex.Replace(text, @"₱|PHP|pesos?|,|\s", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint,
                   CultureInfo.InvariantCulture, out var amount) && amount > 0 &&
               decimal.Round(amount, 2) == amount
            ? amount
            : null;
    }

    private static bool Matches(string reference, string candidate) =>
        string.Equals(reference.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(reference, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(candidate)}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

public sealed record AllowanceAccountCandidate(int Id, string Name, string? Provider);
public sealed record AllowanceMessageAllocation(decimal? Amount, int? AccountId, string AccountReference);
public sealed record AllowanceMessageDraft(decimal? AmountReceived,
    IReadOnlyList<AllowanceMessageAllocation> Allocations, IReadOnlyList<string> Warnings);
