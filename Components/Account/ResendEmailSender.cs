using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Kwenta.Data;

namespace Kwenta.Components.Account;

internal sealed class ResendEmailSender(
    HttpClient httpClient,
    IOptions<ResendOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender<ApplicationUser>
{
    private readonly ResendOptions resendOptions = options.Value;

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendActionEmailAsync(
            email,
            "Confirm your Kwenta account",
            "Confirm email address",
            "Thanks for creating your Kwenta account. Confirm your email address to finish setting up your account.",
            "Confirm email",
            confirmationLink,
            "If you did not create a Kwenta account, you can safely ignore this email.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendActionEmailAsync(
            email,
            "Reset your Kwenta password",
            "Reset your password",
            "We received a request to reset the password for your Kwenta account.",
            "Reset password",
            resetLink,
            "If you did not request a password reset, you can safely ignore this email.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var encodedCode = WebUtility.HtmlEncode(resetCode);
        var html = BuildLayout(
            "Reset your password",
            "Use the following code to reset your Kwenta password:",
            $"<p style=\"margin:24px 0;font-size:24px;font-weight:700;letter-spacing:2px;color:#18212f;\">{encodedCode}</p>",
            "If you did not request a password reset, you can safely ignore this email.");

        return SendEmailAsync(
            email,
            "Reset your Kwenta password",
            html,
            $"Use this code to reset your Kwenta password: {resetCode}\n\nIf you did not request this, you can ignore this email.",
            "password reset code");
    }

    private Task SendActionEmailAsync(
        string email,
        string subject,
        string heading,
        string introduction,
        string buttonText,
        string actionLink,
        string footer)
    {
        var html = BuildLayout(
            heading,
            introduction,
            $"<p style=\"margin:28px 0;\"><a href=\"{actionLink}\" style=\"display:inline-block;padding:12px 20px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:600;\">{WebUtility.HtmlEncode(buttonText)}</a></p>" +
            "<p style=\"margin:0;color:#5b6472;font-size:14px;\">If the button does not work, copy and paste this link into your browser:</p>" +
            $"<p style=\"margin:8px 0 0;overflow-wrap:anywhere;font-size:14px;\"><a href=\"{actionLink}\" style=\"color:#2563eb;\">{actionLink}</a></p>",
            footer);

        var decodedLink = WebUtility.HtmlDecode(actionLink);
        var text = $"{heading}\n\n{introduction}\n\n{buttonText}: {decodedLink}\n\n{footer}";

        return SendEmailAsync(email, subject, html, text, heading.ToLowerInvariant());
    }

    private async Task SendEmailAsync(string email, string subject, string html, string text, string emailType)
    {
        if (string.IsNullOrWhiteSpace(resendOptions.ApiKey) ||
            string.IsNullOrWhiteSpace(resendOptions.FromEmail))
        {
            logger.LogError(
                "Transactional email was not sent because Resend configuration is incomplete. " +
                "Configure Resend:ApiKey and Resend:FromEmail.");
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resendOptions.ApiKey);
            request.Content = JsonContent.Create(new
            {
                from = FormatSender(resendOptions.FromName, resendOptions.FromEmail),
                to = new[] { email },
                subject,
                html,
                text
            });

            using var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Resend rejected the {EmailType} email with HTTP status {StatusCode}.",
                    emailType,
                    (int)response.StatusCode);
                return;
            }

            logger.LogInformation("Sent {EmailType} email through Resend.", emailType);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send {EmailType} email through Resend.", emailType);
        }
    }

    private static string FormatSender(string fromName, string fromEmail)
    {
        var safeName = fromName.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        var safeEmail = fromEmail.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        return string.IsNullOrEmpty(safeName) ? safeEmail : $"{safeName} <{safeEmail}>";
    }

    private static string BuildLayout(string heading, string introduction, string content, string footer) => $$"""
        <!doctype html>
        <html lang="en">
        <body style="margin:0;padding:24px;background:#f4f6f8;font-family:Arial,sans-serif;color:#18212f;">
          <div style="max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:10px;padding:32px;">
            <p style="margin:0 0 24px;font-size:22px;font-weight:700;color:#2563eb;">Kwenta</p>
            <h1 style="margin:0 0 16px;font-size:24px;line-height:1.3;">{{WebUtility.HtmlEncode(heading)}}</h1>
            <p style="margin:0;color:#374151;line-height:1.6;">{{WebUtility.HtmlEncode(introduction)}}</p>
            {{content}}
            <p style="margin:28px 0 0;padding-top:20px;border-top:1px solid #e5e7eb;color:#6b7280;font-size:13px;line-height:1.5;">{{WebUtility.HtmlEncode(footer)}}</p>
          </div>
        </body>
        </html>
        """;
}
