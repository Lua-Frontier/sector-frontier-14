using Content.Client._Lua.Announce;
using Content.Shared._RMC14.Announce;

namespace Content.Client._RMC14.Announce;

public static class AnnouncementDisplayResolver
{
    public static bool TryResolve(
        AnnouncementNetData data,
        AnnouncementDisplayPreference preference,
        out AnnouncementDisplayData resolved)
    {
        resolved = default!;

        if (preference == AnnouncementDisplayPreference.Disabled)
            return false;

        var presentation = AnnouncementPresentationCatalog.Resolve(data.AnnouncementId, preference);
        resolved = new AnnouncementDisplayData
        {
            AnnouncementId = data.AnnouncementId,
            Text = data.Text,
            Priority = data.Priority,
            Presentation = presentation,
            SpeakerEntity = data.SpeakerEntity,
            SpeakerName = data.SpeakerName,
            SpeakerJobTitle = data.SpeakerJobTitle,
            OverrideId = data.OverrideId,
            TitleOverride = data.TitleOverride,
            TitleColorOverride = data.TitleColorOverride,
            TextColorOverride = data.TextColorOverride,
            DecalRsiOverride = data.DecalRsi,
            DecalStateOverride = data.DecalState
        };
        return true;
    }
}
