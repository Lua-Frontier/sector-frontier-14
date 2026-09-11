// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Announcements;
using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Announce.Core;
using Content.Shared._Lua.Announce;
using Content.Shared._Mono.Company;
using Content.Shared._RMC14.Announce;
using Content.Shared.Access.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Announce;

public sealed class LuaAnnouncementOverlaySystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly FactionAnnouncementSystem _factionAnnouncements = default!;
    [Dependency] private readonly SharedIdCardSystem _idCards = default!;
    [Dependency] private readonly AnnouncementOverlaySystem _rmcOverlay = default!;

    public void Dispatch(Filter filter, AnnouncementOverlayParams overlay)
    {
        if (_net.IsClient || filter.Count == 0)
            return;

        if (overlay.ResolvePreset() is not { } preset)
        {
            Log.Warning("Announcement overlay requested without a preset, speaker, or faction.");
            return;
        }

        var lines = AnnouncementLineHelper.NormalizeAndSplit(overlay.Message);
        if (lines.Length == 0)
            return;

        string? speakerName = null;
        string? speakerJobTitle = null;
        if (overlay.Speaker is { } speakerEntity)
        {
            speakerName = Name(speakerEntity);
            speakerJobTitle = AnnouncementOverlayAppearanceHelper.ResolveSpeakerJobTitle(
                _prototypes,
                _idCards,
                speakerEntity);
        }

        var appearance = ResolveAppearanceOverrides(overlay);
        var overrideId = overlay.Speaker != null
            ? _rmcOverlay.EnsureSpeakerPvs(overlay.Speaker, filter)
            : 0u;

        var clientData = new AnnouncementNetData
        {
            Text = lines,
            AnnouncementId = preset,
            Priority = AnnouncementPresetCatalog.GetPriority(preset),
            SpeakerEntity = overlay.Speaker is { } speaker ? GetNetEntity(speaker) : null,
            SpeakerName = speakerName,
            SpeakerJobTitle = speakerJobTitle,
            OverrideId = overrideId,
            TitleOverride = appearance.TitleOverride,
            TitleColorOverride = appearance.TitleColorOverride,
            TextColorOverride = appearance.TextColorOverride,
            DecalRsi = appearance.DecalRsi,
            DecalState = appearance.DecalState
        };

        RaiseNetworkEvent(new AnnouncementNetMessage(clientData), filter);
        _rmcOverlay.LogAnnouncement(
            AnnouncementPresetCatalog.GetId(preset),
            lines,
            overlay.Source,
            filter.Count);
    }

    private AppearanceOverrides ResolveAppearanceOverrides(AnnouncementOverlayParams overlay)
    {
        var result = new AppearanceOverrides();

        if (!string.IsNullOrWhiteSpace(overlay.SenderTitle))
            result.TitleOverride = overlay.SenderTitle;

        if (overlay.ColorOverride is { } color)
        {
            result.TitleColorOverride = color;
            result.TextColorOverride = color;
        }

        if (string.IsNullOrWhiteSpace(overlay.FactionId) ||
            !_factionAnnouncements.TryGetFactionIdentity(
                overlay.FactionId,
                out var factionTitle,
                out var factionColor,
                out _))
        {
            return result;
        }

        result.TitleOverride ??= factionTitle;
        result.TitleColorOverride ??= factionColor;
        result.TextColorOverride ??= factionColor;

        if (_prototypes.TryIndex<CompanyPrototype>(overlay.FactionId, out var company) &&
            AnnouncementOverlayAppearanceHelper.TryResolveCompanyIcon(company, out var rsi, out var state))
        {
            result.DecalRsi = rsi;
            result.DecalState = state;
        }

        return result;
    }

    private sealed class AppearanceOverrides
    {
        public string? TitleOverride;
        public Color? TitleColorOverride;
        public Color? TextColorOverride;
        public string? DecalRsi;
        public string? DecalState;
    }
}
