namespace OscarWatch.Core.Models;

public enum CloudlogUploadStatus
{
    None,
    Pending,
    Sent,
    Failed
}

public static class CloudlogUploadStatusCodec
{
    public static string ToStorage(CloudlogUploadStatus status) => status switch
    {
        CloudlogUploadStatus.Pending => "pending",
        CloudlogUploadStatus.Sent => "sent",
        CloudlogUploadStatus.Failed => "failed",
        _ => "none"
    };

    public static CloudlogUploadStatus FromStorage(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "pending" => CloudlogUploadStatus.Pending,
        "sent" => CloudlogUploadStatus.Sent,
        "failed" => CloudlogUploadStatus.Failed,
        _ => CloudlogUploadStatus.None
    };
}
