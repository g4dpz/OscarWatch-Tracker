using System.Net;

namespace OscarWatch.Core.Services;

public enum HamsAtFetchErrorKind
{
    None,
    MissingApiKey,
    InvalidApiKey,
    RateLimited,
    Timeout,
    Unavailable,
    Network,
    UnexpectedResponse,
    Generic
}

public static class HamsAtErrorHelper
{
    public static string ToEnglish(HamsAtFetchErrorKind kind) => kind switch
    {
        HamsAtFetchErrorKind.MissingApiKey => "API key is required.",
        HamsAtFetchErrorKind.InvalidApiKey => "Invalid API key.",
        HamsAtFetchErrorKind.RateLimited => "Rate limited by hams.at. Try again later.",
        HamsAtFetchErrorKind.Timeout => "Request timed out.",
        HamsAtFetchErrorKind.Unavailable => "hams.at is temporarily unavailable. Try again later.",
        HamsAtFetchErrorKind.Network => "Could not reach hams.at. Check your internet connection.",
        HamsAtFetchErrorKind.UnexpectedResponse => "Unexpected response from hams.at.",
        _ => "Could not load roves from hams.at."
    };

    public static HamsAtFetchErrorKind FromHttpException(HttpRequestException ex)
    {
        if (ex.StatusCode is null)
            return HamsAtFetchErrorKind.Network;

        return FromStatusCode(ex.StatusCode.Value);
    }

    public static HamsAtFetchErrorKind FromStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => HamsAtFetchErrorKind.InvalidApiKey,
            HttpStatusCode.TooManyRequests => HamsAtFetchErrorKind.RateLimited,
            _ when code >= 500 => HamsAtFetchErrorKind.Unavailable,
            _ => HamsAtFetchErrorKind.Generic
        };
    }
}
