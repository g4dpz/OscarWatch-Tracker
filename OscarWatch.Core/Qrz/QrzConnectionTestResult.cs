namespace OscarWatch.Core.Qrz;

public sealed class QrzConnectionTestResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public string? SubscriptionExpires { get; init; }

    public bool SubscriptionRequired { get; init; }

    public static QrzConnectionTestResult Success(string? subscriptionExpires) => new()
    {
        Ok = true,
        SubscriptionExpires = subscriptionExpires
    };

    public static QrzConnectionTestResult Failed(string message, bool subscriptionRequired = false) => new()
    {
        Ok = false,
        ErrorMessage = message,
        SubscriptionRequired = subscriptionRequired
    };
}
