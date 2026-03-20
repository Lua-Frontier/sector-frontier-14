// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using Content.Server._Lua.Stargate.Components;
using Content.Shared._Lua.Stargate;
using Content.Shared._Lua.Stargate.Components;
using Content.Shared._Lua.Stargate.PlanetQuest;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;
namespace Content.Server._Lua.Stargate.Systems;
public sealed class StargateMinimapTabletSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    private EntityQuery<OccluderComponent> _occluderQuery;
    private TimeSpan _nextUpdate;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    // Reusable buffers to avoid per-frame allocations
    private readonly Dictionary<Vector2i, uint[]> _chunksBuffer = new();
    private readonly List<StargateMinimapMarker> _markersBuffer = new();
    private readonly List<Vector2> _questZonesBuffer = new();
    public override void Initialize()
    {
        base.Initialize();
        _occluderQuery = GetEntityQuery<OccluderComponent>();
        SubscribeLocalEvent<StargateMinimapTabletComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<StargateMinimapTabletComponent, StargateMinimapPlaceMarkerMessage>(OnPlaceMarker);
        SubscribeLocalEvent<StargateMinimapTabletComponent, StargateMinimapRemoveMarkerMessage>(OnRemoveMarker);
        SubscribeLocalEvent<StargateMinimapTabletComponent, StargateMinimapMergeDiskMessage>(OnMergeDisk);
        SubscribeLocalEvent<StargateMinimapTabletComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<StargateMinimapTabletComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        SubscribeLocalEvent<StargateMinimapTabletComponent, ComponentInit>(OnTabletInit);
    }
    private void OnTabletInit(EntityUid uid, StargateMinimapTabletComponent comp, ComponentInit args)
    {
        UpdateTabletAppearance(uid);
    }
    private void OnUiOpened(EntityUid uid, StargateMinimapTabletComponent comp, BoundUIOpenedEvent args) { UpdateUiState(uid); }
    private void OnContainerChanged(EntityUid uid, StargateMinimapTabletComponent comp, EntInsertedIntoContainerMessage args) { if (args.Container.ID is "disk_slot_1" or "disk_slot_2") UpdateUiState(uid); }
    private void OnContainerRemoved(EntityUid uid, StargateMinimapTabletComponent comp, EntRemovedFromContainerMessage args) { if (args.Container.ID is "disk_slot_1" or "disk_slot_2") UpdateUiState(uid); }
    private void OnPlaceMarker(EntityUid uid, StargateMinimapTabletComponent comp, StargateMinimapPlaceMarkerMessage args)
    {
        var disk = GetDisk(uid, 1);
        if (disk == null || !TryComp<StargateMinimapDiskComponent>(disk, out var dc)) return;
        var pd = GetCurrentPlanetData(dc);
        if (pd == null) return;
        if (pd.Markers.Count >= 64) return;
        var label = args.Label ?? $"M{pd.Markers.Count + 1}";
        pd.Markers.Add(new StargateMinimapMarker(args.Position, label));
        UpdateUiState(uid);
    }
    private void OnRemoveMarker(EntityUid uid, StargateMinimapTabletComponent comp, StargateMinimapRemoveMarkerMessage args)
    {
        var disk = GetDisk(uid, 1);
        if (disk == null || !TryComp<StargateMinimapDiskComponent>(disk, out var dc)) return;
        var pd = GetCurrentPlanetData(dc);
        if (pd == null) return;
        if (args.Index < 0 || args.Index >= pd.Markers.Count) return;
        pd.Markers.RemoveAt(args.Index);
        UpdateUiState(uid);
    }
    private void OnMergeDisk(EntityUid uid, StargateMinimapTabletComponent comp, StargateMinimapMergeDiskMessage args)
    {
        if (args.FromSlot is not (1 or 2) || args.ToSlot is not (1 or 2) || args.FromSlot == args.ToSlot) return;
        var from = GetDisk(uid, args.FromSlot);
        var to = GetDisk(uid, args.ToSlot);
        if (from == null || to == null) return;
        if (!TryComp<StargateMinimapDiskComponent>(from, out var fc) || !TryComp<StargateMinimapDiskComponent>(to, out var tc)) return;

        foreach (var (planetKey, srcPd) in fc.Planets)
        {
            if (!tc.Planets.TryGetValue(planetKey, out var dstPd))
            {
                dstPd = new StargateMinimapPlanetData();
                tc.Planets[planetKey] = dstPd;
            }

            foreach (var (idx, srcTiles) in srcPd.Chunks)
            {
                if (!dstPd.Chunks.TryGetValue(idx, out var dstTiles)) { dstPd.Chunks[idx] = (uint[])srcTiles.Clone(); continue; }
                for (var i = 0; i < srcTiles.Length && i < dstTiles.Length; i++) { if (srcTiles[i] != 0 && dstTiles[i] == 0) dstTiles[i] = srcTiles[i]; }
            }

            foreach (var m in srcPd.Markers)
            {
                var dup = false;
                foreach (var e in dstPd.Markers) { if (Vector2.DistanceSquared(e.Position, m.Position) < 1f) { dup = true; break; } }
                if (!dup) dstPd.Markers.Add(new StargateMinimapMarker(m.Position, m.Label));
            }
        }

        UpdateUiState(uid);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate) return;
        _nextUpdate = _timing.CurTime + UpdateInterval;
        var query = EntityQueryEnumerator<StargateMinimapTabletComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var player = FindHoldingPlayer(uid);
            if (player == null) continue;
            var xform = Transform(player.Value);
            if (xform.MapUid == null) continue;
            if (!TryComp<StargateDestinationComponent>(xform.MapUid.Value, out var dest)) continue;
            var disk = GetDisk(uid, 1);
            if (disk == null || !TryComp<StargateMinimapDiskComponent>(disk, out var dc)) continue;
            dc.CurrentPlanetAddress = dest.Address;
            var key = AddressKey(dest.Address);
            if (!dc.Planets.TryGetValue(key, out var pd))
            {
                pd = new StargateMinimapPlanetData();
                dc.Planets[key] = pd;
            }
            if (!TryComp<MapGridComponent>(xform.MapUid.Value, out var grid)) continue;
            ExploreTiles(xform.MapUid.Value, grid, xform, pd);
            UpdateUiState(uid);
        }
    }
    private void ExploreTiles(EntityUid mapUid, MapGridComponent grid, TransformComponent playerXform, StargateMinimapPlanetData pd)
    {
        var pos = _xform.GetWorldPosition(playerXform);
        var center = new Vector2i((int)MathF.Floor(pos.X), (int)MathF.Floor(pos.Y));
        var r = StargateMinimapConstants.ExploreRadius;
        var rSq = r * r;
        var refreshR = StargateMinimapConstants.RefreshRadius;
        var refreshRSq = refreshR * refreshR;
        for (var dx = -r; dx <= r; dx++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                if (dx * dx + dy * dy > rSq) continue;
                var tp = center + new Vector2i(dx, dy);
                var distSq = dx * dx + dy * dy;
                if (distSq > refreshRSq && IsTileSet(pd, tp)) continue;
                var tileRef = _maps.GetTileRef(mapUid, grid, tp);
                if (tileRef.Tile.IsEmpty) continue;
                var tileDef = (ContentTileDefinition)_tileDef[tileRef.Tile.TypeId];
                var color = StargateMinimapConstants.PackColor(GetTileColor(tileDef));
                var anchored = _maps.GetAnchoredEntitiesEnumerator(mapUid, grid, tp);
                var hasLiquid = false;
                while (anchored.MoveNext(out var ent))
                {
                    var protoId = MetaData(ent.Value).EntityPrototype?.ID;
                    if (protoId == "FloorWaterEntity") { color = StargateMinimapConstants.PackColor(Color.FromHex("#66B2FF")); hasLiquid = true; break; }
                    if (protoId == "FloorLavaEntity") { color = StargateMinimapConstants.PackColor(Color.FromHex("#f56d05")); hasLiquid = true; break; }
                    if (protoId == "FloorLiquidPlasmaEntity") { color = StargateMinimapConstants.PackColor(Color.FromHex("#af68ba")); hasLiquid = true; break; }
                }
                if (!hasLiquid)
                {
                    anchored = _maps.GetAnchoredEntitiesEnumerator(mapUid, grid, tp);
                    while (anchored.MoveNext(out var ent))
                    {
                        if (_occluderQuery.TryComp(ent.Value, out var occ) && occ.Enabled) { color = StargateMinimapConstants.WallColor; break; }
                    }
                }
                SetTileValue(pd, tp, color);
            }
        }
    }
    private static Color GetTileColor(ContentTileDefinition d)
    {
        if (d.MapAtmosphere) return Color.FromHex("#050510");
        var id = d.ID;
        if (id.Contains("Grass") || id.Contains("Jungle")) return Color.FromHex("#3a6b35");
        if (id.Contains("Dirt")) return Color.FromHex("#6b4e30");
        if (id.Contains("Sand")) return Color.FromHex("#c2b280");
        if (id.Contains("Snow") || id.Contains("Ice")) return Color.FromHex("#d4e6f1");
        if (id.Contains("Wood")) return Color.FromHex("#6b4226");
        if (id.Contains("Steel") || id.Contains("Metal")) return Color.FromHex("#5a5a5a");
        if (id.Contains("Lava") || id.Contains("Magma")) return Color.FromHex("#f56d05");
        if (id.Contains("Plasma")) return Color.FromHex("#af68ba");
        if (id.Contains("Water") || id.Contains("Hydro") || id.Contains("FloorWater")) return Color.FromHex("#66B2FF");
        if (id.Contains("Asteroid")) return Color.FromHex("#4a3728");
        if (id.Contains("Basalt")) return Color.FromHex("#3a3a3a");
        if (id.Contains("Chromite")) return Color.FromHex("#5a5a6a");
        if (id.Contains("Andesite")) return Color.FromHex("#6a6a6a");
        if (id.Contains("Dark")) return Color.FromHex("#2a2a2a");
        if (id.Contains("White")) return Color.FromHex("#d0d0d0");
        if (id.Contains("Gold")) return Color.FromHex("#c5a042");
        if (id.Contains("Plating")) return Color.FromHex("#4a4a4a");
        if (id.Contains("Lattice")) return Color.FromHex("#333333");
        return Color.FromHex("#3d3d3d");
    }
    private static bool IsTileSet(StargateMinimapPlanetData pd, Vector2i tp)
    {
        var cs = StargateMinimapConstants.ChunkSize;
        var ci = new Vector2i(FloorDiv(tp.X, cs), FloorDiv(tp.Y, cs));
        if (!pd.Chunks.TryGetValue(ci, out var tiles)) return false;
        return tiles[(tp.Y - ci.Y * cs) * cs + (tp.X - ci.X * cs)] != 0;
    }
    private static void SetTileValue(StargateMinimapPlanetData pd, Vector2i tp, uint color)
    {
        var cs = StargateMinimapConstants.ChunkSize;
        var ci = new Vector2i(FloorDiv(tp.X, cs), FloorDiv(tp.Y, cs));
        if (!pd.Chunks.TryGetValue(ci, out var tiles)) { tiles = new uint[StargateMinimapConstants.ChunkTileCount]; pd.Chunks[ci] = tiles; }
        tiles[(tp.Y - ci.Y * cs) * cs + (tp.X - ci.X * cs)] = color;
    }
    private static int FloorDiv(int v, int s) { return v >= 0 ? v / s : (v - s + 1) / s; }
    private EntityUid? FindHoldingPlayer(EntityUid uid)
    {
        var cur = Transform(uid).ParentUid;
        for (var i = 0; i < 10 && cur.IsValid(); i++) { if (HasComp<ActorComponent>(cur)) return cur; cur = Transform(cur).ParentUid; }
        return null;
    }
    private void UpdateUiState(EntityUid uid)
    {
        var player = FindHoldingPlayer(uid);
        var isSg = false;
        Vector2? gatePos = null;
        Vector2? playerPos = null;
        if (player != null)
        {
            var xform = Transform(player.Value);
            if (xform.MapUid != null && TryComp<StargateDestinationComponent>(xform.MapUid.Value, out var dest))
            {
                isSg = true;
                if (dest.GateUid != null && TryComp<TransformComponent>(dest.GateUid.Value, out var gx)) gatePos = _xform.GetWorldPosition(gx);
                playerPos = _xform.GetWorldPosition(xform);
            }
        }
        var d1 = GetDisk(uid, 1);
        var d2 = GetDisk(uid, 2);
        // Reuse buffers instead of allocating new collections each call
        _chunksBuffer.Clear();
        _markersBuffer.Clear();
        if (d1 != null && TryComp<StargateMinimapDiskComponent>(d1, out var dc))
        {
            var pd = GetCurrentPlanetData(dc);
            if (pd != null)
            {
                // No Clone() needed: SetUiState serializes immediately, so server data won't be mutated by client
                foreach (var (k, v) in pd.Chunks) _chunksBuffer[k] = v;
                _markersBuffer.AddRange(pd.Markers);
            }
        }
        CollectQuestTargetZones(player, isSg, _questZonesBuffer);
        _ui.SetUiState(uid, StargateMinimapTabletUiKey.Key, new StargateMinimapUiState(isSg, d1 != null, d2 != null, _chunksBuffer, _markersBuffer, gatePos, playerPos, _questZonesBuffer.Count > 0 ? _questZonesBuffer : null));
        UpdateTabletAppearance(uid);
    }
    private void UpdateTabletAppearance(EntityUid uid)
    {
        var hasDisk = GetDisk(uid, 1) != null || GetDisk(uid, 2) != null;
        _appearance.SetData(uid, StargateMinimapTabletVisuals.HasDisk, hasDisk);
    }
    private static string AddressKey(byte[] address)
    {
        return string.Join("-", address);
    }

    private static StargateMinimapPlanetData? GetCurrentPlanetData(StargateMinimapDiskComponent dc)
    {
        if (dc.CurrentPlanetAddress.Length == 0)
            return null;
        dc.Planets.TryGetValue(AddressKey(dc.CurrentPlanetAddress), out var pd);
        return pd;
    }

    private const float QuestZoneRadius = 21f;
    private const float QuestZoneMaxOffset = 14f;

    private void CollectQuestTargetZones(EntityUid? player, bool isSg, List<Vector2> zones)
    {
        zones.Clear();
        if (!isSg || player == null)
            return;

        var xform = Transform(player.Value);
        if (xform.MapUid == null)
            return;

        var mapUid = xform.MapUid.Value;
        if (!TryComp<PlanetQuestComponent>(mapUid, out var quest) || quest.Completed)
            return;

        var targetQuery = EntityQueryEnumerator<PlanetQuestTargetComponent, TransformComponent>();
        while (targetQuery.MoveNext(out var uid, out var target, out var targetXform))
        {
            if (target.QuestMap != mapUid)
                continue;
            if (TryComp<MobStateComponent>(uid, out var mobState) &&
                mobState.CurrentState != MobState.Alive)
            {
                continue;
            }

            var realPos = _xform.GetWorldPosition(targetXform);

            // Reuse seeded Random by value — no allocation needed
            var rng = new Random(uid.Id);
            var angle = rng.NextDouble() * Math.PI * 2;
            var dist = rng.NextDouble() * QuestZoneMaxOffset;
            var offset = new Vector2((float)(Math.Cos(angle) * dist), (float)(Math.Sin(angle) * dist));

            zones.Add(realPos + offset);
        }
    }

    private EntityUid? GetDisk(EntityUid uid, int slot)
    {
        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            return null;
        var id = slot == 1 ? "disk_slot_1" : "disk_slot_2";
        return _itemSlots.TryGetSlot(uid, id, out var s, itemSlots) ? s.Item : null;
    }
}
