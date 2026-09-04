using Content.Shared.Database;

namespace Content.Server._RMC14.Announce;

public sealed partial class AnnouncementOverlaySystem
{
    public void LogAnnouncement(
        string announcementId,
        string[] text,
        EntityUid? source,
        int recipientCount)
    {
        var sourceStr = source?.ToString() ?? "System";
        var textPreview = text.Length > 0 ? text[0] : string.Empty;
        if (textPreview.Length > 50)
            textPreview = textPreview[..47] + "...";

        _adminLogs.Add(LogType.AdminMessage, LogImpact.Medium,
            $"Announcement [{announcementId}] from {sourceStr} ({recipientCount} recipients): {textPreview}");
    }
}
