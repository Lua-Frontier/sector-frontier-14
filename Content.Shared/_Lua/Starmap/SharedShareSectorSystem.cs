// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Starmap.Components;
using Content.Shared._Mono.Company;
using Content.Shared.ActionBlocker;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using System;

namespace Content.Shared._Lua.Starmap;

public abstract class SharedShareSectorSystem : EntitySystem
{
    public const float ShareRange = 10f;

    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KnownSectorsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnGetAlternativeVerbs(Entity<KnownSectorsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        if (user != ent.Owner || !args.CanAccess || !args.CanInteract || !_blocker.CanInteract(user, ent.Owner))
            return;

        AlternativeVerb verb = new()
        {
            Act = () => _ui.TryOpenUi(ent.Owner, ShareSectorUiKey.Key, user),
            Text = Loc.GetString("share-sector-verb-text"),
            Priority = 2
        };
        args.Verbs.Add(verb);
    }

    public HashSet<string> GetKnownSectorIds(
        EntityUid uid,
        ComposedStarmapData data,
        string? companyId,
        bool globallyUnlocked,
        KnownSectorsComponent? known = null)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in data.Stars)
        {
            if (string.Equals(def.StarType, "decorative", StringComparison.OrdinalIgnoreCase))
                continue;

            if (SectorVisibility.IsSectorVisible(def, companyId, globallyUnlocked))
                result.Add(def.Id);
        }

        if (!Resolve(uid, ref known, false))
            return result;

        foreach (var sectorId in known.LearnedSectorIds)
        {
            if (string.IsNullOrWhiteSpace(sectorId))
                continue;

            if (TryGetSector(data, sectorId, out var learnedDef)
                && string.Equals(learnedDef.StarType, "decorative", StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(sectorId);
        }

        return result;
    }

    private static bool TryGetSector(ComposedStarmapData data, string sectorId, out StarDefinition def)
    {
        foreach (var star in data.Stars)
        {
            if (!string.Equals(star.Id, sectorId, StringComparison.OrdinalIgnoreCase))
                continue;
            def = star;
            return true;
        }

        def = null!;
        return false;
    }

    public bool KnowsSector(
        EntityUid uid,
        ComposedStarmapData data,
        string sectorId,
        string? companyId,
        bool globallyUnlocked,
        KnownSectorsComponent? known = null)
    {
        if (TryGetSector(data, sectorId, out var def)
            && string.Equals(def.StarType, "decorative", StringComparison.OrdinalIgnoreCase))
            return false;

        if (SectorVisibility.IsSectorVisible(data, sectorId, companyId, globallyUnlocked))
            return true;

        if (!Resolve(uid, ref known, false))
            return false;

        foreach (var learned in known.LearnedSectorIds)
        {
            if (string.Equals(learned, sectorId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
