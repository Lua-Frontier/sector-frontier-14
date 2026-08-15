// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Company;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Content.Shared._Mono.Company;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared._Lua.Chat.Systems;
using Content.Shared.Lua.CLVar;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Globalization;
using System.Numerics;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class ShareSectorSystem : SharedShareSectorSystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly FactionWarSystem _factionWar = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        Subs.BuiEvents<KnownSectorsComponent>(ShareSectorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<ShareSectorSelectedMessage>(OnShareSectorSelected);
        });
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        EnsureComp<KnownSectorsComponent>(args.Mob);
    }

    private void OnUiOpened(Entity<KnownSectorsComponent> ent, ref BoundUIOpenedEvent args)
    {
        PushState(ent.Owner);
    }

    private void OnShareSectorSelected(Entity<KnownSectorsComponent> ent, ref ShareSectorSelectedMessage args)
    {
        var speaker = ent.Owner;
        if (string.IsNullOrWhiteSpace(args.SectorId) || !TryGetStarmapData(out var data))
            return;

        var company = GetCompany(speaker);
        var globallyUnlocked = _factionWar.AreFactionSectorsUnlocked();
        if (!KnowsSector(speaker, data, args.SectorId, company, globallyUnlocked, ent.Comp))
            return;

        if (!TryGetSector(data, args.SectorId, out var sector))
            return;

        var recipients = GrantNearby(speaker, args.SectorId, company, globallyUnlocked, data);
        var chatMessage = Loc.GetString(
            "share-sector-chat-message",
            ("x", FormatCoordinate(sector.Position.X)),
            ("y", FormatCoordinate(sector.Position.Y)));

        _chat.TrySendInGameICMessage(speaker, chatMessage, InGameICChatType.Speak, ChatTransmitRange.Normal, false);
        _popup.PopupEntity(Loc.GetString("share-sector-popup-speaker", ("count", recipients)), speaker, speaker);
    }

    private int GrantNearby(
        EntityUid speaker,
        string sectorId,
        string? speakerCompany,
        bool globallyUnlocked,
        ComposedStarmapData data)
    {
        if (!TryComp<TransformComponent>(speaker, out var speakerXform))
            return 0;

        var speakerPos = speakerXform.WorldPosition;
        var count = 0;
        var query = EntityQueryEnumerator<ActorComponent, HumanoidAppearanceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (uid == speaker || xform.MapID != speakerXform.MapID)
                continue;

            if (Vector2.Distance(speakerPos, xform.WorldPosition) > ShareRange)
                continue;

            var known = EnsureComp<KnownSectorsComponent>(uid);
            var recipientCompany = GetCompany(uid);
            if (KnowsSector(uid, data, sectorId, recipientCompany, globallyUnlocked, known))
                continue;

            known.LearnedSectorIds.Add(sectorId);
            Dirty(uid, known);
            count++;
            _popup.PopupEntity(Loc.GetString("share-sector-popup-recipient"), uid, uid);
        }

        return count;
    }

    private void PushState(EntityUid uid)
    {
        if (!TryGetStarmapData(out var data))
            return;

        TryComp<KnownSectorsComponent>(uid, out var known);
        var company = GetCompany(uid);
        var globallyUnlocked = _factionWar.AreFactionSectorsUnlocked();
        var knownIds = GetKnownSectorIds(uid, data, company, globallyUnlocked, known);
        var entries = new List<ShareSectorEntry>();

        foreach (var def in data.Stars)
        {
            if (!knownIds.Contains(def.Id))
                continue;

            entries.Add(new ShareSectorEntry(
                def.Id,
                GetSectorNameLocKey(def.Id),
                FormatCoordinate(def.Position.X),
                FormatCoordinate(def.Position.Y),
                def.VisibleCompanies));
        }

        entries.Sort((a, b) => string.Compare(a.NameLocKey, b.NameLocKey, StringComparison.OrdinalIgnoreCase));
        _ui.SetUiState(uid, ShareSectorUiKey.Key, new ShareSectorBoundUserInterfaceState(entries));
    }

    private static string GetSectorNameLocKey(string sectorId)
    {
        return $"share-sector-name-{sectorId}";
    }

    private static string FormatCoordinate(float coordinate)
    {
        return coordinate.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private string GetCompany(EntityUid uid)
    {
        if (TryComp<CompanyComponent>(uid, out var company) && !string.IsNullOrWhiteSpace(company.CompanyName))
            return company.CompanyName;

        return SectorVisibility.NoneCompany;
    }

    private bool TryGetStarmapData(out ComposedStarmapData data)
    {
        var dataId = _configurationManager.GetCVar(CLVars.StarmapDataId);
        return StarmapDataComposer.TryCompose(_prototypeManager, dataId, out data!);
    }

    private static bool TryGetSector(ComposedStarmapData data, string sectorId, out StarDefinition sector)
    {
        foreach (var def in data.Stars)
        {
            if (!string.Equals(def.Id, sectorId, StringComparison.OrdinalIgnoreCase))
                continue;

            sector = def;
            return true;
        }

        sector = default!;
        return false;
    }
}
