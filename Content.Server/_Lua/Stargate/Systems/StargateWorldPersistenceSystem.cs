// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Content.Server._Lua.AutoSalarySystem;
using Content.Server._Lua.HardsuitSpeedBuff;
using Content.Server._Lua.Stargate.Components;
using Content.Server._NF.Roles.Systems;
using Content.Server._NF.RoundNotifications.Events;
using Content.Server.GameTicking;
using Content.Server._NF.Shipyard.Systems;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Spawners.EntitySystems;
using Content.Shared._Goobstation.Vehicles;
using Content.Shared._Lua.HardsuitIdentification;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Components;
using Content.Shared.Clothing.Components;
using Content.Server.CombatMode;
using Content.Server.Light.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Lua.CLVar;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Speech.Components;
using Content.Shared.Store.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;
using SharpZstd.Interop;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateWorldPersistenceSystem : EntitySystem
{
    public const string SaveDirectory = "stargate_saves";
    public const string Extension = ".rtsave";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly NPCRetaliationSystem _npcRetaliation = default!;
    [Dependency] private readonly KillTrackingSystem _killTracking = default!;
    [Dependency] private readonly JobTrackingSystem _jobTracking = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly ConditionalSpawnerSystem _conditionalSpawner = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly HandheldLightSystem _handheldLight = default!;
    [Dependency] private readonly MaskSystem _mask = default!;
    [Dependency] private readonly ToggleClothingSystem _toggleClothing = default!;
    [Dependency] private readonly NPCUseActionOnTargetSystem _npcUseAction = default!;
    [Dependency] private readonly ActionGrantSystem _actionGrant = default!;
    [Dependency] private readonly CombatModeSystem _combatMode = default!;

    private ZStdCompressionContext? _zstdContext;

    public override void Initialize()
    {
        base.Initialize();
        _mapLoader.OnIsSerializable += OnMapLoaderIsSerializable;
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    public override void Shutdown()
    {
        _mapLoader.OnIsSerializable -= OnMapLoaderIsSerializable;
        _zstdContext?.Dispose();
        _zstdContext = null;
        base.Shutdown();
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!_cfg.GetCVar(CLVars.StargateWorldClearSavesOnRoundEnd)) return;
        ClearAllSaves();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PostRound) return;
        if (!_cfg.GetCVar(CLVars.StargateWorldClearSavesOnRoundEnd)) return;
        ClearAllSaves();
    }

    private void ClearAllSaves()
    {
        var dir = new ResPath($"/{SaveDirectory}");
        if (!_resource.UserData.IsDir(dir))
        {
            Log.Debug("StarGate save directory does not exist, nothing to clear");
            return;
        }
        var deleted = 0;
        foreach (var name in _resource.UserData.DirectoryEntries(dir))
        {
            if (!name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) continue;
            var filePath = dir / name;
            _resource.UserData.Delete(filePath);
            deleted++;
        }
        Log.Info("Cleared {Count} StarGate save file(s) from {Dir}", deleted, dir);
    }

    private void OnMapLoaderIsSerializable(Entity<MetaDataComponent> ent, ref bool serializable)
    {
        if (!SavingStargateWorld) return;
        serializable = true;
        if (TryComp<AttachedClothingComponent>(ent, out var attached) && Exists(attached.AttachedUid))
        {
            var xformQ = GetEntityQuery<TransformComponent>();
            var parent = attached.AttachedUid;
            for (var depth = 0; depth < 4 && parent.IsValid(); depth++)
            {
                if (TryComp<MobStateComponent>(parent, out var parentMob) && parentMob.CurrentState is MobState.Dead or MobState.Critical && !HasComp<MindContainerComponent>(parent))
                {
                    serializable = false;
                    return;
                }
                parent = xformQ.TryGetComponent(parent, out var px) ? px.ParentUid : EntityUid.Invalid;
            }
        }

        if (!TryComp<MobStateComponent>(ent, out var mobState)) return;
        switch (mobState.CurrentState)
        {
            case MobState.Alive: break;
            case MobState.Dead:
            case MobState.Critical:
                if (!HasComp<MindContainerComponent>(ent)) serializable = false;
                break;
        }
    }

    internal static bool SavingStargateWorld;

    public static ResPath GetSavePath(string addressKey)
    { return new ResPath($"{SaveDirectory}/{addressKey}{Extension}"); }

    public bool SaveExists(string addressKey)
    {
        var path = GetSavePath(addressKey).ToRootedPath();
        return _resource.UserData.Exists(path);
    }

    public bool TrySaveStargateWorld(EntityUid mapUid, ResPath path)
    {
        if (!HasComp<StargateDestinationComponent>(mapUid)) return false;
        _npcRetaliation.ClearAllAttackMemories();
        _killTracking.ClearAllLifetimeDamage();
        ClearStationReferences(mapUid);
        _mind.ClearMindReferencesOnMap(mapUid);
        DeleteActionEntities(mapUid);
        _shipyard.ClearShuttleDeedReferencesOnMap(mapUid);
        PruneNpcEntityRefs(mapUid);
        CleanStaleContainerRefs(mapUid);
        var projectileQuery = AllEntityQuery<TargetedProjectileComponent, TransformComponent>();
        while (projectileQuery.MoveNext(out var projUid, out _, out var xform))
        { if (xform.MapUid == mapUid) QueueDel(projUid); }
        _conditionalSpawner.MarkPersistentSpawnersAsSpawnedOnMap(mapUid);
        var opts = SerializationOptions.Default with
        {
            Category = FileCategory.Map,
            MissingEntityBehaviour = MissingEntityBehaviour.Error
        };
        MappingDataNode data;
        FileCategory cat;
        try
        {
            SavingStargateWorld = true;
            try
            { (data, cat) = _mapLoader.SerializeEntitiesRecursive([mapUid], opts); }
            finally
            { SavingStargateWorld = false; }
        }
        catch (Exception ex)
        {
            Log.Error("Failed to serialize StarGate world {Map} for save: {Ex}", mapUid, ex);
            return false;
        }
        if (cat != FileCategory.Map)
        {
            Log.Error("Serialized StarGate world {Map} is not a map (got {Category})", mapUid, cat);
            return false;
        }
        return WriteRtsave(path, data);
    }

    public bool TryLoadStargateWorld(ResPath path, [NotNullWhen(true)] out LoadResult? result)
    {
        result = null;
        if (!TryReadRtsave(path, out var data)) return false;
        var opts = MapLoadOptions.Default with { ExpectedCategory = FileCategory.Map };
        if (!_mapLoader.TryLoadGeneric(data, path.ToString(), out result, opts)) return false;
        foreach (var map in result.Maps)
        { if (TryComp<StargateDestinationComponent>(map.Owner, out var dest)) dest.Frozen = true; }
        return true;
    }

    private bool WriteRtsave(ResPath path, MappingDataNode data)
    {
        var yamlString = MappingDataNodeToYamlString(data);
        var uncompressed = System.Text.Encoding.UTF8.GetBytes(yamlString);
        _zstdContext ??= new ZStdCompressionContext();
        _zstdContext.SetParameter(ZSTD_cParameter.ZSTD_c_compressionLevel, _cfg.GetCVar(CLVars.StargateWorldSaveCompressLevel));
        path = path.ToRootedPath();
        _resource.UserData.CreateDir(path.Directory);
        var bound = ZStd.CompressBound(uncompressed.Length);
        var buf = ArrayPool<byte>.Shared.Rent(4 + bound);
        try
        {
            var compressedLength = _zstdContext.Compress2(buf.AsSpan(4, bound), uncompressed.AsSpan());
            if (!BitConverter.TryWriteBytes(buf.AsSpan(0, 4), uncompressed.Length)) return false;
            using var stream = _resource.UserData.OpenWrite(path);
            stream.Write(buf.AsSpan(0, 4 + compressedLength));
        }
        finally
        { ArrayPool<byte>.Shared.Return(buf); }
        Log.Info("Saved StarGate world to {Path}", path);
        return true;
    }

    private bool TryReadRtsave(ResPath path, [NotNullWhen(true)] out MappingDataNode? data)
    {
        data = null;
        path = path.ToRootedPath();
        if (!_resource.UserData.Exists(path)) return false;
        using var fileStream = _resource.UserData.OpenRead(path);
        var lengthBuf = new byte[4];
        if (fileStream.Read(lengthBuf, 0, 4) != 4) return false;
        var uncompressedSize = BitConverter.ToInt32(lengthBuf);
        if (uncompressedSize <= 0 || uncompressedSize > 100 * 1024 * 1024)
        {
            Log.Error("Invalid uncompressed size in .rtsave: {Size}", uncompressedSize);
            return false;
        }
        using var decompressStream = new ZStdDecompressStream(fileStream, ownStream: false);
        using var decompressed = new MemoryStream(uncompressedSize);
        decompressStream.CopyTo(decompressed);
        decompressed.Position = 0;
        if (decompressed.Length != uncompressedSize)
        {
            Log.Error("Decompressed size mismatch: expected {Expected}, got {Actual}", uncompressedSize, decompressed.Length);
            return false;
        }
        using var reader = new StreamReader(decompressed, System.Text.Encoding.UTF8);
        var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
        if (documents.Length == 0)
        {
            Log.Error("No YAML document in .rtsave");
            return false;
        }
        if (documents.Length > 1)
        {
            Log.Error("Multiple YAML documents in .rtsave");
            return false;
        }
        data = (MappingDataNode)documents[0].Root;
        return true;
    }

    private static string MappingDataNodeToYamlString(MappingDataNode data)
    {
        using var writer = new StringWriter();
        var document = new YamlDocument(data.ToYaml());
        var stream = new YamlStream { document };
        stream.Save(new YamlMappingFix(new Emitter(writer)), false);
        return writer.ToString();
    }

    private void ClearStationReferences(EntityUid mapUid)
    {
        _jobTracking.ClearStationReferencesOnMap(mapUid);
        var salaryQuery = AllEntityQuery<SalaryTrackingComponent, TransformComponent>();
        while (salaryQuery.MoveNext(out _, out var salary, out var xform))
        {
            if (xform.MapUid != mapUid) continue;
            salary.Station = EntityUid.Invalid;
        }
        var storeQuery = AllEntityQuery<StoreComponent>();
        while (storeQuery.MoveNext(out _, out var store))
        {
            store.AccountOwner = null;
            store.BoughtEntities.Clear();
            store.StartingMap = null;
        }
    }

    private void DeleteActionEntities(EntityUid mapUid)
    {
        {
            var q = AllEntityQuery<ActionsComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; _actions.ClearGrantedActions(uid, c); }
        }
        {
            var q = AllEntityQuery<MobStateActionsComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; c.GrantedActions.Clear(); }
        }
        {
            var q = AllEntityQuery<ActionGrantComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; _actionGrant.ClearActionEntities(uid, c); }
        }
        {
            var q = AllEntityQuery<IntrinsicUIComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; foreach (var entry in c.UIs.Values) entry.ToggleActionEntity = null; Dirty(uid, c); }
        }
        {
            var q = AllEntityQuery<CombatModeComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; _combatMode.ClearActionEntity(uid, c); }
        }
        {
            var q = AllEntityQuery<VocalComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; c.ScreamActionEntity = null; Dirty(uid, c); }
        }
        {
            var q = AllEntityQuery<HandheldLightComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; _handheldLight.ClearActionEntities(uid, c); }
        }
        {
            var q = AllEntityQuery<GasTankComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; c.ToggleActionEntity = null; Dirty(uid, c); }
        }
        {
            var q = AllEntityQuery<ToggleClothingComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; _toggleClothing.ClearActionEntity(uid, c); }
        }
        {
            var q = AllEntityQuery<MaskComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; _mask.ClearActionEntity(uid, c); }
        }
        {
            var q = AllEntityQuery<JetpackComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; c.ToggleActionEntity = null; Dirty(uid, c); }
        }
        {
            var q = AllEntityQuery<HardsuitDNARadialComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; c.OpenDNARadialActionEntity = null; }
        }
        {
            var q = AllEntityQuery<HealOnBuckleComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; c.SleepAction = null; Dirty(uid, c); }
        }
        {
            var q = AllEntityQuery<NPCUseActionOnTargetComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; _npcUseAction.ClearActionEntity(uid, c); }
        }
        {
            var q = AllEntityQuery<UnpoweredFlashlightComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; c.ToggleActionEntity = null; Dirty(uid, c); }
        }
        {
            var q = AllEntityQuery<VehicleComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            {
                if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue;
                c.Driver = null;
                c.Passenger = null;
                c.HornAction = null;
                c.SirenAction = null;
                Dirty(uid, c);
            }
        }
        {
            var q = AllEntityQuery<HardsuitSpeedBuffComponent, TransformComponent>();
            while (q.MoveNext(out var uid, out var c, out var xform))
            { if (xform.MapUid != mapUid && !IsEntityOnMap(uid, mapUid)) continue; c.ActionEntity = null; }
        }
        {
            var toDelete = new List<EntityUid>();
            var xformQuery2 = GetEntityQuery<TransformComponent>();
            {
                var q = AllEntityQuery<ActionComponent>();
                while (q.MoveNext(out var uid, out var action))
                {
                    var container = action.Container ?? action.AttachedEntity;
                    if (container == null) continue;
                    if (!xformQuery2.TryGetComponent(container.Value, out var ownerXform)) continue;
                    if (ownerXform.MapUid != mapUid && !IsEntityOnMap(container.Value, mapUid)) continue;
                    toDelete.Add(uid);
                }
            }
            foreach (var uid in toDelete) Del(uid);
        }
    }
    private void PruneNpcEntityRefs(EntityUid mapUid)
    {
        var imprintQuery = AllEntityQuery<NPCImprintingOnSpawnBehaviourComponent, TransformComponent>();
        while (imprintQuery.MoveNext(out _, out var imprint, out var xform))
        {
            if (xform.MapUid != mapUid) continue;
            imprint.Friends.RemoveAll(uid => !Exists(uid) || !IsEntityOnMap(uid, mapUid));
        }
        var factionQuery = AllEntityQuery<FactionExceptionComponent, TransformComponent>();
        while (factionQuery.MoveNext(out var factionUid, out var faction, out var xform))
        {
            if (xform.MapUid != mapUid) continue;
            _npcFaction.PruneStaleExceptions(factionUid, faction, e => !Exists(e) || !IsEntityOnMap(e, mapUid));
        }
    }

    private bool IsEntityOnMap(EntityUid uid, EntityUid mapUid)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        if (!xformQuery.TryGetComponent(uid, out var xform)) return false;
        if (xform.MapUid == mapUid) return true;
        var parent = xform.ParentUid;
        while (parent.IsValid())
        {
            if (parent == mapUid) return true;
            if (!xformQuery.TryGetComponent(parent, out var parentXform)) return false;
            parent = parentXform.ParentUid;
        }
        return false;
    }
    private void CleanStaleContainerRefs(EntityUid mapUid)
    {
        var query = AllEntityQuery<ContainerManagerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var manager, out var xform))
        {
            if (xform.MapUid != mapUid) continue;
            foreach (var container in _container.GetAllContainers(uid, manager))
            {
                foreach (var contained in container.ContainedEntities.ToArray())
                { if (!Exists(contained)) _container.RemoveDeletedEntity(contained, container); }
            }
        }
    }
}
