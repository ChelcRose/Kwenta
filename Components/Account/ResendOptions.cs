namespace Kwenta.Components.Account;

internal sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string? ApiKey { get; set; }

    public string? FromEmail { get; set; }

    public string FromName { get; set; } = "Kwenta";
}
