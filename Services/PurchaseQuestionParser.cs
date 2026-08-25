using System.Globalization;
using System.Text.RegularExpressions;

namespace Kwenta.Services;

public static partial class PurchaseQuestionParser
{
    private const string AmountPattern = @"(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?";

    [GeneratedRegex(
        @"^can\s+i\s+(?:buy|afford)\s+(?:₱|php\s*)(?<amount>" + AmountPattern + @")\s+(?<name>.+?)\s*[?.!]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountFirstPattern();

    [GeneratedRegex(
        @"^can\s+i\s+(?:buy|afford)\s+(?<name>.+?)\s+for\s+(?:₱|php\s*)(?<amount>" + AmountPattern + @")\s*[?.!]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountLastPattern();

    [GeneratedRegex(
        @"^(?:help\s+me\s+save\s+for|how\s+can\s+i\s+save\s+for|make\s+me\s+a?\s*plan\s+for)\s+(?:a\s+)?(?:₱|php\s*)(?<amount>" + AmountPattern + @")\s+(?<name>.+?)\s*[?.!]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PurchasePlanPattern();

    [GeneratedRegex(@"^can\s+i\s+(?:buy|afford)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PurchaseIntentPattern();

    [GeneratedRegex(
        @"^(?:help\s+me\s+save\s+for|how\s+can\s+i\s+save\s+for|make\s+me\s+a?\s*plan\s+for)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PurchasePlanIntentPattern();

    [GeneratedRegex(@"(?:₱|php\s*)(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyAmountPattern();

    public static bool LooksLikePurchaseQuestion(string question) =>
        PurchaseIntentPattern().IsMatch(question.Trim());

    public static bool LooksLikePurchasePlanQuestion(string question) =>
        PurchasePlanIntentPattern().IsMatch(question.Trim());

    public static bool TryParsePlan(string question, out PurchaseRequest purchase)
    {
        purchase = null!;
        var input = question.Trim();

        if (CurrencyAmountPattern().Matches(input).Count != 1)
        {
            return false;
        }

        return TryCreatePurchase(PurchasePlanPattern().Match(input), out purchase);
    }

    public static bool TryParse(string question, out PurchaseRequest purchase)
    {
        purchase = null!;
        var input = question.Trim();

        if (CurrencyAmountPattern().Matches(input).Count != 1)
        {
            return false;
        }

        var match = AmountFirstPattern().Match(input);
        if (!match.Success)
        {
            match = AmountLastPattern().Match(input);
        }

        if (!match.Success)
        {
            return false;
        }

        return TryCreatePurchase(match, out purchase);
    }

    private static bool TryCreatePurchase(Match match, out PurchaseRequest purchase)
    {
        purchase = null!;
        if (!match.Success)
        {
            return false;
        }

        var name = match.Groups["name"].Value.Trim().TrimEnd('?', '.', '!').Trim();
        var amountText = match.Groups["amount"].Value;

        if (name.Length == 0 ||
            CurrencyAmountPattern().IsMatch(name) ||
            !decimal.TryParse(
                amountText,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var amount) ||
            amount <= 0)
        {
            return false;
        }

        purchase = new PurchaseRequest(name, amount);
        return true;
    }
}

public sealed record PurchaseRequest(string Name, decimal Amount);
