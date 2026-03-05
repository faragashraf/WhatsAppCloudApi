using System.Text.RegularExpressions;

namespace WhatsAppCloudApi.Shared.Logging;

public static class LogSanitizer
{
    // Match "Bearer <token>" where token can be any non-whitespace characters (covers unicode including Arabic)
    private static readonly Regex BearerRegex = new("Bearer\\s+(\\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Match JSON access_token values: "access_token": "..."
    private static readonly Regex AccessTokenJsonRegex = new("\"access_token\"\\s*:\\s*\"(.*?)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static string MaskSensitive(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        // Mask Bearer tokens (handles tokens with Unicode characters)
        var result = BearerRegex.Replace(input, "Bearer ***");

        // Mask any JSON access_token values
        result = AccessTokenJsonRegex.Replace(result, "\"access_token\": \"***\"");

        return result;
    }
}
