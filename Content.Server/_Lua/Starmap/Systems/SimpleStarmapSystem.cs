// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Company;
using Content.Server._Lua.Sectors;
using Content.Server._Lua.SpaceHazards;
using Content.Server._Lua.Starmap.Components;
using Content.Server._Lua.Shuttles.Systems;
using Content.Server.Popups;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Content.Shared._Mono.Company;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.Lua.CLVar;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Server._Lua.Starmap.Systems
{
    public sealed class SimpleStarmapSystem : EntitySystem
    {
        [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly StarmapSystem _starmap = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly SectorSystem _sectors = default!;
        [Dependency] private readonly SharedContainerSystem _containers = default!;
        [Dependency] private readonly FactionWarSystem _factionWar = default!;
        [Dependency] private readonly NebulaEnvironmentSystem _nebulaEnvironment = default!;
        [Dependency] private readonly ShuttleGridAccessSystem _gridAccess = default!;

        private const int SectorArrivalAttempts = 48;
        private readonly List<(AmbientSpaceFieldComponent Field, Vector2 Position)> _ftlBlockingFields = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<FTLCompletedEvent>(OnFtlCompleted);
        }

        public void WarpToStar(EntityUid consoleUid, Star star, EntityUid? actor = null)
        {
            if (!TryComp<TransformComponent>(consoleUid, out var consoleTransform)) { return; }
            var shuttleUid = consoleTransform.GridUid;
            if (shuttleUid == null) { return; }
            if (!_gridAccess.TryGetShuttleGrid(shuttleUid.Value, out var shuttleComponent)) { return; }
            if (!star.CanWarp)
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-decorative-no-warp"), consoleUid); return; }
            if (HasComp<WarpTransitComponent>(shuttleUid.Value))
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("shuttle-console-in-ftl"), consoleUid); return; }
            if (!_mapManager.MapExists(star.Map))
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
            var mapUid = _mapManager.GetMapEntityId(star.Map);
            if (star.Position == Vector2.Zero)
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-already-here"), consoleUid); return; }
            var currentMap = consoleTransform.MapID;
            var stars = _starmap.CollectStars();
            _sectors.TryGetCentComMapId(out var centComMap);
            _sectors.TryGetHubMapId(out var hubMap);
            var isCentComTarget = centComMap != MapId.Nullspace && star.Map == centComMap;
            var isInCentCom = centComMap != MapId.Nullspace && currentMap == centComMap;
            if (!isCentComTarget)
            {
                if (isInCentCom)
                {
                    if (hubMap == MapId.Nullspace || star.Map != hubMap)
                    { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
                }
                else if (!IsAdjacentByHyperlane(currentMap, star, stars))
                { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
            }
            if (isCentComTarget && !_sectors.CentComStarUnlocked && !HasComp<AllowFtlToCentComComponent>(shuttleUid.Value))
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
            if (!CanAccessSector(consoleUid, star.Map, actor))
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
            if (!_shuttleSystem.TryGetBluespaceDrive(shuttleUid.Value, out var warpDriveUid, out var warpDrive) || warpDriveUid == null)
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-warpdrive"), consoleUid); return; }
            void PlayDenySound(EntityUid uid)
            { _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg"), uid); }
            if (!_shuttleSystem.CanFTL(shuttleUid.Value, out var reason))
            { PlayDenySound(consoleUid); if (!string.IsNullOrEmpty(reason)) _popup.PopupEntity(reason!, consoleUid); return; }
            _nebulaEnvironment.CollectFtlBlockingFields(star.Map, _ftlBlockingFields);
            Vector2 targetPos = default;
            EntityCoordinates targetCoordinates = default;
            var foundSafeSpot = false;
            for (var attempt = 0; attempt < SectorArrivalAttempts; attempt++)
            {
                var angle = (float)(_random.NextDouble() * 2 * Math.PI);
                var radius = _random.Next(1000, 5001);
                var offset = new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
                var candidate = star.Position + offset;
                if (NebulaEnvironmentSystem.IsFtlBlockedByFields(candidate, _ftlBlockingFields))
                    continue;
                targetPos = candidate;
                targetCoordinates = new EntityCoordinates(mapUid, candidate);
                foundSafeSpot = true;
                break;
            }

            if (!foundSafeSpot)
            {
                PlayDenySound(consoleUid);
                _popup.PopupEntity(Loc.GetString("nebula-ftl-blocked"), consoleUid);
                return;
            }

            _shuttleSystem.FTLToCoordinates(shuttleUid.Value, shuttleComponent, targetCoordinates, Angle.Zero);
            if (!HasComp<FTLComponent>(shuttleUid.Value))
            { PlayDenySound(consoleUid); return; }
            var transit = EnsureComp<WarpTransitComponent>(shuttleUid.Value);
            transit.TargetMap = star.Map;
            transit.TargetPosition = targetPos;
            Dirty(shuttleUid.Value, transit);
            try { EntityManager.System<StarmapSystem>().RefreshConsoles(); } catch { }
        }

        private bool CanAccessSector(EntityUid consoleUid, MapId targetMap, EntityUid? actor)
        {
            var company = SectorVisibility.NoneCompany;
            IReadOnlyCollection<string>? learned = null;
            if (actor != null &&
                actor.Value.IsValid() &&
                TryComp<CompanyComponent>(actor.Value, out var companyComp) &&
                !string.IsNullOrWhiteSpace(companyComp.CompanyName))
                company = companyComp.CompanyName;

            if (actor != null &&
                actor.Value.IsValid() &&
                TryComp<KnownSectorsComponent>(actor.Value, out var known))
                learned = known.LearnedSectorIds;

            var globallyUnlocked = _factionWar.AreFactionSectorsUnlocked();
            var sectorId = ResolveSectorId(targetMap);
            if (sectorId != null)
            {
                try
                {
                    var dataId = _configurationManager.GetCVar(CLVars.StarmapDataId);
                    if (StarmapDataComposer.TryCompose(_prototypeManager, dataId, out var data) &&
                        !SectorVisibility.IsSectorVisible(data, sectorId, company, globallyUnlocked, learned))
                        return false;
                }
                catch
                {
                    return false;
                }
            }
            if (!_configurationManager.GetCVar(CLVars.StarmapRequireSectorDisks))
                return true;

            if (_sectors.TryGetHubMapId(out var hubMap) && targetMap == hubMap)
                return true;
            if (_sectors.TryGetCentComMapId(out var centComMap) && targetMap == centComMap)
                return true;
            if (!_containers.TryGetContainer(consoleUid, "disk_slot", out var diskCont) || diskCont.ContainedEntities.Count == 0)
                return false;
            var disk = diskCont.ContainedEntities[0];
            if (!TryComp<StarMapCoordinatesDiskComponent>(disk, out var diskComp) || diskComp.AllowedSectorIds.Count == 0)
                return false;
            foreach (var sid in diskComp.AllowedSectorIds)
            {
                if (string.IsNullOrWhiteSpace(sid))
                    continue;
                if (!_sectors.TryGetMapId(sid, out var mapId))
                    continue;

                if (mapId == targetMap)
                    return true;
            }

            return false;
        }

        private string? ResolveSectorId(MapId targetMap)
        {
            if (_sectors.TryGetSectorId(targetMap, out var sectorId))
                return sectorId;
            return null;
        }

        private bool IsAdjacentByHyperlane(MapId currentMap, Star target, List<Star> stars)
        {
            var edges = _starmap.GetHyperlanesCached();
            var centerIndex = stars.FindIndex(s => s.Map == currentMap);
            var targetIndex = stars.FindIndex(s => s.Map == target.Map);
            if (centerIndex == -1) return false;
            if (targetIndex == -1) return false;
            foreach (var e in edges)
            { if ((e.A == centerIndex && e.B == targetIndex) || (e.B == centerIndex && e.A == targetIndex)) return true; }
            return false;
        }

        private void OnFtlCompleted(ref FTLCompletedEvent ev)
        {
            var shuttle = ev.Entity;
            if (!TryComp<WarpTransitComponent>(shuttle, out var transit)) return;
            RemCompDeferred<WarpTransitComponent>(shuttle);
            var mapUid = _mapManager.GetMapEntityId(transit.TargetMap);
            var targetCoords = new EntityCoordinates(mapUid, transit.TargetPosition);
            _shuttleSystem.TryFTLProximity((shuttle, Transform(shuttle)), targetCoords);
            var xform = Transform(shuttle);
            var arrived = xform.WorldPosition;
            _nebulaEnvironment.CollectFtlBlockingFields(transit.TargetMap, _ftlBlockingFields);
            if (NebulaEnvironmentSystem.IsFtlBlockedByFields(arrived, _ftlBlockingFields))
            {
                if (!NebulaEnvironmentSystem.IsFtlBlockedByFields(transit.TargetPosition, _ftlBlockingFields))
                {
                    _shuttleSystem.TryFTLProximity((shuttle, xform), new EntityCoordinates(mapUid, transit.TargetPosition));
                }
                else
                {
                    for (var attempt = 0; attempt < SectorArrivalAttempts; attempt++)
                    {
                        var angle = (float)(_random.NextDouble() * 2 * Math.PI);
                        var radius = _random.Next(250, 2001);
                        var candidate = arrived + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
                        if (NebulaEnvironmentSystem.IsFtlBlockedByFields(candidate, _ftlBlockingFields))
                            continue;
                        _shuttleSystem.TryFTLProximity((shuttle, xform), new EntityCoordinates(mapUid, candidate));
                        break;
                    }
                }
            }

            if (TryComp<WarpTransitComponent>(shuttle, out var arriving))
            {
                Dirty(shuttle, arriving);
                Timer.Spawn(TimeSpan.FromSeconds(2), () => { if (TryComp<WarpTransitComponent>(shuttle, out var still)) RemCompDeferred<WarpTransitComponent>(shuttle); });
            }
            try { EntityManager.System<ShuttleConsoleSystem>().RefreshShuttleConsoles(shuttle); } catch { }
            try { EntityManager.System<StarmapSystem>().RefreshConsoles(); } catch { }
        }
    }
}
