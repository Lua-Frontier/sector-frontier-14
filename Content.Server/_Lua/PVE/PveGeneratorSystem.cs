// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Sectors;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking.Events;
using Content.Shared.Alert;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Lua.CLVar;
using Content.Shared.Lua.PirateIcon.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Linq;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.LW.PveSector
{
    public sealed class PveDefaultMapSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly SectorSystem _sectors = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedMindSystem _mindSystem = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly SharedAudioSystem _audioManager = default!;
        [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

        private const string PveAlert = "Pve";

        private bool _pveEnabled = true;

        private readonly HashSet<EntityUid> _trackedEntities = new();
        private readonly HashSet<EntityUid> _detectedPirates = new();
        private readonly Dictionary<EntityUid, bool> _pirateStateCache = new();

        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(20);

        private TimeSpan _nextScan;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
            _cfg.OnValueChanged(CLVars.PveEnabled, OnPveEnabledChanged, true);
        }

        private void OnPveEnabledChanged(bool value)
        {
            _pveEnabled = value;
            if (!_pveEnabled)
            {
                ClearAllAlerts();
            }
        }

        private void OnRoundStart(RoundStartingEvent ev)
        {
            _trackedEntities.Clear();
            _detectedPirates.Clear();
            _nextScan = _gameTiming.CurTime + ScanInterval;
        }
        private void OnCleanup(RoundRestartCleanupEvent ev)
        {
            foreach (var ent in _trackedEntities)
            {
                _alerts.ClearAlert(ent, PveAlert);
            }
            _trackedEntities.Clear();
            _detectedPirates.Clear();
        }

        public override void Update(float frameTime)
        {
            if (!_pveEnabled)
                return;

            base.Update(frameTime);

            if (_gameTiming.CurTime < _nextScan)
                return;

            _nextScan = _gameTiming.CurTime + ScanInterval;

            var mapId = _sectors.TryGetHubMapId(out var hub) ? hub : MapId.Nullspace;
            if (mapId == MapId.Nullspace || !_mapManager.MapExists(mapId))
            {
                ClearAllAlerts();
                return;
            }

            var newList = new List<EntityUid>();
            var query = AllEntityQuery<HumanoidAppearanceComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (!_mindSystem.TryGetMind(uid, out _, out _))
                    continue;

                if (xform.MapID == mapId)
                {
                    newList.Add(uid);
                }
            }

            var exited = _trackedEntities.Except(newList);
            foreach (var e in exited)
            {
                _alerts.ClearAlert(e, PveAlert);
            }

            var entered = newList.Except(_trackedEntities);
            foreach (var e in entered)
            {
                _alerts.ShowAlert(e, PveAlert);
            }

            _trackedEntities.Clear();
            _trackedEntities.UnionWith(newList);

            /*
            var pirateQuery = AllEntityQuery<PirateIconComponent, TransformComponent, MobStateComponent>();
            while (pirateQuery.MoveNext(out var pid, out _, out var pxform, out var mobState))
            {
                if (pxform.MapID != mapId)
                    continue;

                if (_detectedPirates.Contains(pid))
                    continue;

                var isInvalid = _mobStateSystem.IsDead(pid, mobState) || _mobStateSystem.IsCritical(pid, mobState);

                if (_pirateStateCache.TryGetValue(pid, out var cachedState))
                {
                    if (cachedState && !isInvalid)
                    {
                        continue;
                    }
                }

                _pirateStateCache[pid] = isInvalid;

                if (isInvalid)
                {
                    continue;
                }

                _detectedPirates.Add(pid);

                var coords = pxform.MapPosition;

                var x = (int) coords.Position.X;
                var y = (int) coords.Position.Y;

                var message = $"Внимание! В секторе NanoTrasen зафиксированы неопознанные гуманоиды вероятно враждебной фракции. Автоматическая система Блюспейс Артиллерии активирована. Приготовиться к удару. " +
                    $"Службе безопасности проследовать по координатам ({x}, {y})!";

                _chatSystem.DispatchGlobalAnnouncement(
                    message: message,
                    colorOverride: new Color(255, 0, 0)
                );

                var pirateUid = pid;

                Timer.Spawn(TimeSpan.FromSeconds(5), () =>
                {
                    _audioManager.PlayGlobal("/Audio/DeadSpace/Artillery/ARTA1.ogg", Filter.Broadcast(), true);
                });

                Timer.Spawn(TimeSpan.FromSeconds(15), () =>
                {
                    if (!Exists(pirateUid))
                        return;

                    var currentXform = Transform(pirateUid);
                    var currentCoords = currentXform.MapPosition;

                    _explosionSystem.QueueExplosion(
                        currentCoords,
                        "Cryo",
                        5000f,
                        5f,
                        5000f
                    );
                });

                Timer.Spawn(TimeSpan.FromSeconds(21), () =>
                {
                    _audioManager.PlayGlobal("/Audio/DeadSpace/Artillery/Shockwave1.ogg", Filter.Broadcast(), true);
                });

                Timer.Spawn(TimeSpan.FromSeconds(24), () =>
                {
                    if (!Exists(pirateUid))
                        return;

                    var currentXform = Transform(pirateUid);
                    var currentCoords = currentXform.MapPosition;

                    _explosionSystem.QueueExplosion(
                        currentCoords,
                        "Cryo",
                        5000f,
                        5f,
                        5000f
                    );
                });
            }
            */
        }
        private void ClearAllAlerts()
        {
            foreach (var ent in _trackedEntities)
            {
                _alerts.ClearAlert(ent, PveAlert);
            }
            _trackedEntities.Clear();
        }
    }
}
