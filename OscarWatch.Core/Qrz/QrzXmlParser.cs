using System.Xml.Linq;

namespace OscarWatch.Core.Qrz;

public static class QrzXmlParser
{
    public static QrzXmlSession ParseSession(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return new QrzXmlSession();

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return new QrzXmlSession();
        }

        var session = FindElement(document, "Session");
        if (session is null)
            return new QrzXmlSession();

        return new QrzXmlSession
        {
            Key = ElementText(session, "Key"),
            Error = ElementText(session, "Error"),
            SubscriptionExpires = ElementText(session, "SubExp"),
            Message = ElementText(session, "Message")
        };
    }

    public static QrzCallbookEntry? ParseCallsign(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var callsign = FindElement(document, "Callsign");
        if (callsign is null)
            return null;

        var call = ElementText(callsign, "call");
        if (string.IsNullOrWhiteSpace(call))
            return null;

        var first = ElementText(callsign, "fname");
        var last = ElementText(callsign, "name");
        var name = !string.IsNullOrWhiteSpace(first) ? first : last;

        return new QrzCallbookEntry
        {
            Call = call.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Grid = ElementText(callsign, "grid").Trim().ToUpperInvariant()
        };
    }

    public static bool IsSessionTimeout(string? error) =>
        !string.IsNullOrWhiteSpace(error)
        && error.Contains("timeout", StringComparison.OrdinalIgnoreCase);

    public static bool IsNotFound(string? error) =>
        !string.IsNullOrWhiteSpace(error)
        && (error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || error.Contains("notfound", StringComparison.OrdinalIgnoreCase));

    public static bool IsSubscriptionRequired(string? error) =>
        !string.IsNullOrWhiteSpace(error)
        && (error.Contains("subscription", StringComparison.OrdinalIgnoreCase)
            || error.Contains("subscriber", StringComparison.OrdinalIgnoreCase)
            || error.Contains("xml data", StringComparison.OrdinalIgnoreCase));

    private static XElement? FindElement(XDocument document, string localName) =>
        document.Descendants().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    private static string ElementText(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim() ?? "";
}

public sealed class QrzXmlSession
{
    public string Key { get; init; } = "";

    public string Error { get; init; } = "";

    public string SubscriptionExpires { get; init; } = "";

    public string Message { get; init; } = "";

    public bool HasKey => !string.IsNullOrWhiteSpace(Key);
}
