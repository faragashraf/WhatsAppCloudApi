using System.Globalization;
using System.Net;
using System.Text;

namespace WhatsAppCloudApi.Infrastructure.Services;

internal static class BrandEmailTemplateRenderer
{
    private const string BrandName = "Bot Global Service";
    private const string BrandUrl = "https://www.botglobalservice.com/";
    private const string BrandSupportEmail = "support@botglobalservice.com";

    public static string RenderPasswordResetOtp(string? recipientName, string otp, TimeSpan expiresIn)
    {
        var greetingName = string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName.Trim();
        var minutes = Math.Max(1, (int)Math.Ceiling(expiresIn.TotalMinutes));

        var body = BuildParagraph("We received a request to reset your Bot Global Service password.")
            + BuildParagraph("Use the one-time password below in the reset form. For your security, do not share this code.");

        return RenderLayout(new BrandedEmailLayout
        {
            Preheader = $"Your password reset code is ready. It expires in {minutes} minute(s).",
            Eyebrow = "SECURITY ALERT",
            Title = "Password Reset Verification",
            Subtitle = "Use this one-time code to continue the reset process.",
            Greeting = $"Hello {greetingName},",
            BodyHtml = body,
            SpotlightLabel = "One-time password",
            SpotlightValue = otp,
            SpotlightHint = $"Expires in {minutes} minute(s)",
            Metrics =
            [
                new BrandEmailMetric("Code length", "6 digits"),
                new BrandEmailMetric("Validity", $"{minutes} minute(s)"),
                new BrandEmailMetric("Brand", BrandName)
            ],
            CtaLabel = "Open Bot Global Service",
            CtaUrl = BrandUrl,
            FooterNote = "If you did not request this reset, you can safely ignore this email."
        });
    }

    public static string RenderSubscriptionExpiryNotice(
        string? companyName,
        string? companyEmail,
        DateTime expiryDate,
        int daysUntilExpiry,
        string renderedBody,
        bool bodyIsHtml)
    {
        var safeCompanyName = string.IsNullOrWhiteSpace(companyName) ? "your company" : companyName.Trim();
        var safeCompanyEmail = string.IsNullOrWhiteSpace(companyEmail) ? "-" : companyEmail.Trim();
        var expiryLabel = expiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var daysLabel = daysUntilExpiry switch
        {
            < 0 => $"Expired {-daysUntilExpiry} day(s) ago",
            0 => "Expires today",
            1 => "1 day remaining",
            _ => $"{daysUntilExpiry} days remaining"
        };

        var body = string.IsNullOrWhiteSpace(renderedBody)
            ? BuildParagraph($"The subscription for {safeCompanyName} is set to expire on {expiryLabel}.")
            : ToHtmlBody(renderedBody, bodyIsHtml);

        if (!string.IsNullOrWhiteSpace(companyEmail))
        {
            body += BuildParagraph($"Primary contact: {safeCompanyEmail}");
        }

        return RenderLayout(new BrandedEmailLayout
        {
            Preheader = $"Subscription notice for {safeCompanyName}: {daysLabel}.",
            Eyebrow = "SUBSCRIPTION NOTICE",
            Title = "Upcoming Subscription Expiry",
            Subtitle = "Review and renew to avoid any interruption.",
            Greeting = $"Hello {safeCompanyName} team,",
            BodyHtml = body,
            SpotlightLabel = "Expiry date",
            SpotlightValue = expiryLabel,
            SpotlightHint = daysLabel,
            Metrics =
            [
                new BrandEmailMetric("Company", safeCompanyName),
                new BrandEmailMetric("Contact", safeCompanyEmail),
                new BrandEmailMetric("Status", daysLabel)
            ],
            CtaLabel = "Visit Bot Global Service",
            CtaUrl = BrandUrl,
            FooterNote = "This is an automated lifecycle message from Bot Global Service."
        });
    }

    private static string RenderLayout(BrandedEmailLayout layout)
    {
        var sb = new StringBuilder(capacity: 8_192);
        var encodedPreheader = WebUtility.HtmlEncode(layout.Preheader);
        var encodedEyebrow = WebUtility.HtmlEncode(layout.Eyebrow);
        var encodedTitle = WebUtility.HtmlEncode(layout.Title);
        var encodedSubtitle = WebUtility.HtmlEncode(layout.Subtitle);
        var encodedGreeting = WebUtility.HtmlEncode(layout.Greeting);
        var encodedSpotlightLabel = WebUtility.HtmlEncode(layout.SpotlightLabel);
        var encodedSpotlightValue = WebUtility.HtmlEncode(layout.SpotlightValue);
        var encodedSpotlightHint = WebUtility.HtmlEncode(layout.SpotlightHint);
        var encodedCtaLabel = WebUtility.HtmlEncode(layout.CtaLabel);
        var encodedFooterNote = WebUtility.HtmlEncode(layout.FooterNote);
        var encodedYear = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        var normalizedCtaUrl = NormalizeUrl(layout.CtaUrl);
        var metricsHtml = BuildMetricsHtml(layout.Metrics);

        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine($"  <title>{encodedTitle}</title>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body style=\"margin:0;padding:0;background:#ecf3f7;font-family:'Segoe UI',Tahoma,Arial,sans-serif;color:#0f172a;\">");
        sb.AppendLine($"  <div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;\">{encodedPreheader}</div>");
        sb.AppendLine("  <table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#ecf3f7;padding:24px 10px;\">");
        sb.AppendLine("    <tr>");
        sb.AppendLine("      <td align=\"center\">");
        sb.AppendLine("        <table role=\"presentation\" width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:640px;width:100%;background:#ffffff;border-radius:20px;overflow:hidden;border:1px solid #d9e4ea;\">");
        sb.AppendLine("          <tr>");
        sb.AppendLine("            <td style=\"padding:26px 30px;background:linear-gradient(135deg,#0f766e 0%,#0ea5a0 62%,#22d3ee 100%);color:#ffffff;\">");
        sb.AppendLine($"              <div style=\"font-size:11px;letter-spacing:0.16em;font-weight:700;opacity:0.94;\">{encodedEyebrow}</div>");
        sb.AppendLine($"              <h1 style=\"margin:10px 0 8px;font-size:30px;line-height:1.2;font-weight:800;\">{encodedTitle}</h1>");
        sb.AppendLine($"              <p style=\"margin:0;font-size:14px;line-height:1.6;opacity:0.95;\">{encodedSubtitle}</p>");
        sb.AppendLine("            </td>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("          <tr>");
        sb.AppendLine("            <td style=\"padding:28px 30px 20px;\">");
        sb.AppendLine($"              <p style=\"margin:0 0 14px;font-size:16px;line-height:1.6;color:#0f172a;\">{encodedGreeting}</p>");
        sb.AppendLine(layout.BodyHtml);
        sb.AppendLine("              <table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:20px 0 10px;\">");
        sb.AppendLine("                <tr>");
        sb.AppendLine("                  <td style=\"border-radius:14px;background:#effcf9;border:1px solid #bff0e9;padding:16px 18px;\">");
        sb.AppendLine($"                    <div style=\"font-size:12px;letter-spacing:0.1em;text-transform:uppercase;color:#0f766e;font-weight:700;\">{encodedSpotlightLabel}</div>");
        sb.AppendLine($"                    <div style=\"margin-top:8px;font-size:34px;line-height:1.1;letter-spacing:0.04em;font-weight:800;color:#0b3f3a;\">{encodedSpotlightValue}</div>");
        sb.AppendLine($"                    <div style=\"margin-top:8px;font-size:13px;line-height:1.5;color:#115e59;\">{encodedSpotlightHint}</div>");
        sb.AppendLine("                  </td>");
        sb.AppendLine("                </tr>");
        sb.AppendLine("              </table>");
        sb.AppendLine(metricsHtml);
        sb.AppendLine("              <table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-top:18px;\">");
        sb.AppendLine("                <tr>");
        sb.AppendLine("                  <td align=\"left\">");
        sb.AppendLine($"                    <a href=\"{normalizedCtaUrl}\" style=\"display:inline-block;padding:12px 20px;border-radius:999px;background:#0f766e;color:#ffffff;text-decoration:none;font-size:13px;font-weight:700;\">{encodedCtaLabel}</a>");
        sb.AppendLine("                  </td>");
        sb.AppendLine("                </tr>");
        sb.AppendLine("              </table>");
        sb.AppendLine("            </td>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("          <tr>");
        sb.AppendLine("            <td style=\"padding:18px 30px 22px;background:#f4f9fc;border-top:1px solid #e2edf3;\">");
        sb.AppendLine($"              <p style=\"margin:0 0 8px;font-size:12px;line-height:1.6;color:#476174;\">{encodedFooterNote}</p>");
        sb.AppendLine($"              <p style=\"margin:0;font-size:12px;line-height:1.6;color:#64748b;\">{WebUtility.HtmlEncode(BrandName)} | <a href=\"{WebUtility.HtmlEncode(BrandUrl)}\" style=\"color:#0f766e;text-decoration:none;\">{WebUtility.HtmlEncode(BrandUrl)}</a> | <a href=\"mailto:{WebUtility.HtmlEncode(BrandSupportEmail)}\" style=\"color:#0f766e;text-decoration:none;\">{WebUtility.HtmlEncode(BrandSupportEmail)}</a> | (c) {encodedYear}</p>");
        sb.AppendLine("            </td>");
        sb.AppendLine("          </tr>");
        sb.AppendLine("        </table>");
        sb.AppendLine("      </td>");
        sb.AppendLine("    </tr>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string BuildMetricsHtml(IReadOnlyList<BrandEmailMetric> metrics)
    {
        if (metrics.Count == 0)
        {
            return string.Empty;
        }

        var width = Math.Max(1, 100 / metrics.Count);
        var sb = new StringBuilder(capacity: 2_048);
        sb.AppendLine("              <table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-top:8px;border-spacing:0;\">");
        sb.AppendLine("                <tr>");
        foreach (var metric in metrics)
        {
            var label = WebUtility.HtmlEncode(metric.Label);
            var value = WebUtility.HtmlEncode(metric.Value);
            sb.AppendLine($"                  <td width=\"{width}%\" valign=\"top\" style=\"padding:10px 10px 10px 0;\">");
            sb.AppendLine("                    <div style=\"border:1px solid #ddebf2;border-radius:12px;padding:10px 12px;background:#ffffff;\">");
            sb.AppendLine($"                      <div style=\"font-size:11px;line-height:1.3;color:#64748b;text-transform:uppercase;letter-spacing:0.08em;font-weight:700;\">{label}</div>");
            sb.AppendLine($"                      <div style=\"margin-top:6px;font-size:14px;line-height:1.5;color:#0f172a;font-weight:700;word-break:break-word;\">{value}</div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                  </td>");
        }
        sb.AppendLine("                </tr>");
        sb.AppendLine("              </table>");
        return sb.ToString();
    }

    private static string BuildParagraph(string text)
    {
        var encoded = WebUtility.HtmlEncode(text ?? string.Empty);
        return $"<p style=\"margin:0 0 12px;font-size:15px;line-height:1.7;color:#1e293b;\">{encoded}</p>";
    }

    private static string ToHtmlBody(string body, bool bodyIsHtml)
    {
        if (bodyIsHtml)
        {
            return body;
        }

        var encoded = WebUtility.HtmlEncode(body ?? string.Empty)
            .Replace("\r\n", "<br />", StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal);

        return $"<p style=\"margin:0 0 12px;font-size:15px;line-height:1.7;color:#1e293b;\">{encoded}</p>";
    }

    private static string NormalizeUrl(string? url)
    {
        var raw = string.IsNullOrWhiteSpace(url) ? BrandUrl : url.Trim();
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return BrandUrl;
        }

        return uri.Scheme is "http" or "https"
            ? uri.ToString()
            : BrandUrl;
    }

    private sealed class BrandedEmailLayout
    {
        public string Preheader { get; set; } = string.Empty;
        public string Eyebrow { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Greeting { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public string SpotlightLabel { get; set; } = string.Empty;
        public string SpotlightValue { get; set; } = string.Empty;
        public string SpotlightHint { get; set; } = string.Empty;
        public IReadOnlyList<BrandEmailMetric> Metrics { get; set; } = [];
        public string CtaLabel { get; set; } = string.Empty;
        public string CtaUrl { get; set; } = string.Empty;
        public string FooterNote { get; set; } = string.Empty;
    }

    private sealed record BrandEmailMetric(string Label, string Value);
}
