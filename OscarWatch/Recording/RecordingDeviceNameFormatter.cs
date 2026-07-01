namespace OscarWatch.Recording;

/// <summary>
/// Formats PortAudio device names for display, removing verbose driver paths and cleaning up formatting.
/// </summary>
internal static class RecordingDeviceNameFormatter
{
    /// <summary>
    /// Formats a raw PortAudio device name into a user-friendly display name.
    /// 
    /// Examples:
    /// - "Headset (@System32\drivers\...\(Soundcore P3))" → "Soundcore P3"
    /// - "IC-910 (Main) (USB Audio CODEC" → "IC-910 (Main) (USB Audio CODEC)"
    /// - "Primary Sound Capture Driver" → "Primary Sound Capture Driver"
    /// </summary>
    internal static string Format(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return rawName ?? "";

        var formatted = rawName.Trim();

        // Extract friendly name from @System32 pattern
        // Examples:
        // - "Headset (@System32\drivers\bthhefenum.sys,#2;%1 Hands-Free%0 ;(Soundcore P3))" 
        //   → "Soundcore P3"
        // - "Input (@System32\...;(IC-910 (Main)))"
        //   → "IC-910 (Main)"
        
        if (formatted.Contains("@System32"))
        {
            // Find the last occurrence of '(' and its matching ')'
            var lastOpenParen = formatted.LastIndexOf('(');
            if (lastOpenParen > 0)
            {
                // Find the first ')' after the last '('
                var closeParenAfterLastOpen = formatted.IndexOf(')', lastOpenParen);
                if (closeParenAfterLastOpen > lastOpenParen)
                {
                    var extractedName = formatted.Substring(lastOpenParen + 1, closeParenAfterLastOpen - lastOpenParen - 1).Trim();
                    if (!string.IsNullOrEmpty(extractedName))
                        return NormalizeWhitespace(extractedName);
                }
            }
        }

        // Fix malformed parentheses: "IC-910 (Main) (USB Audio CODEC" → "IC-910 (Main) (USB Audio CODEC)"
        var openParens = formatted.Count(c => c == '(');
        var closeParens = formatted.Count(c => c == ')');
        if (openParens > closeParens)
        {
            formatted += new string(')', openParens - closeParens);
        }

        return NormalizeWhitespace(formatted);
    }

    /// <summary>
    /// Normalizes whitespace in device names:
    /// - Removes trailing spaces before closing parentheses
    /// - Collapses multiple spaces
    /// - Ensures consistent formatting for deduplication
    /// </summary>
    private static string NormalizeWhitespace(string text)
    {
        // Remove spaces before closing parentheses: "CODEC )" → "CODEC)"
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+\)", ")");
        
        // Remove spaces after opening parentheses: "( CODEC" → "(CODEC"
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\(\s+", "(");
        
        // Collapse multiple spaces to single space
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        
        return text.Trim();
    }
}
