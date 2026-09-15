using System.Xml.Linq;
using OscarWatch.Core.Qrz;

namespace OscarWatch.Core.HamQth;

public static class HamQthXmlParser
{
    public static HamQthXmlSession ParseSession(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return new HamQthXmlSession();

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return new HamQthXmlSession();
        }

        var session = FindElement(document, "session");
        if (session is null)
            return new HamQthXmlSession();

        return new HamQthXmlSession
        {
            SessionId = ElementText(session, "session_id"),
            Error = ElementText(session, "error")
        };
    }

    public static QrzCallbookEntry? ParseSearch(string xml)
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

        var search = FindElement(document, "search");
        if (search is null)
            return null;

        var call = ElementText(search, "callsign");
        if (string.IsNullOrWhiteSpace(call))
            return null;

        var nick = ElementText(search, "nick");
        var addressName = ElementText(search, "adr_name");
        var name = !string.IsNullOrWhiteSpace(nick) ? nick : addressName;

        return new QrzCallbookEntry
        {
            Call = call.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Grid = ElementText(search, "grid").Trim().ToUpperInvariant()
        };
    }

    public static bool IsSessionExpired(string? error) =>
        !string.IsNullOrWhiteSpace(error)
        && (error.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || error.Contains("does not exist", StringComparison.OrdinalIgnoreCase));

    public static bool IsNotFound(string? error) =>
        !string.IsNullOrWhiteSpace(error)
        && error.Contains("not found", StringComparison.OrdinalIgnoreCase);

    private static XElement? FindElement(XDocument document, string localName) =>
        document.Descendants().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    private static string ElementText(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim() ?? "";
}

public sealed class HamQthXmlSession
{
    public string SessionId { get; init; } = "";

    public string Error { get; init; } = "";

    public bool HasSession => !string.IsNullOrWhiteSpace(SessionId);
}
