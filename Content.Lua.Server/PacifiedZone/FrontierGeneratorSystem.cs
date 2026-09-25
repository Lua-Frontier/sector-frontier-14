// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Timing;
using Content.Shared.Alert;
using Content.Shared.Humanoid;
using Content.Shared.Mind;

namespace Content.Server._NF.FrontierZone
{
    public sealed class FrontierZoneGeneratorSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedMindSystem _mindSystem = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;

        private const string Alert = "Frontier";
        private readonly HashSet<EntityUid> _trackedNewBuffer = new();
        private readonly List<EntityUid> _trackedRemoveBuffer = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<FrontierZoneGeneratorComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<FrontierZoneGeneratorComponent, ComponentShutdown>(OnComponentShutdown);
        }

        private void OnComponentInit(EntityUid uid, FrontierZoneGeneratorComponent component, ComponentInit args)
        {
            foreach (var humanoidUid in _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(Transform(uid).Coordinates, component.Radius))
            {
                if (!_mindSystem.TryGetMind(humanoidUid, out var mindId, out var _))
                    continue;

                EnableAlert(humanoidUid);
                AddComp<FrontierZoneComponent>(humanoidUid);
                component.TrackedEntities.Add(humanoidUid);
            }

            component.NextUpdate = _gameTiming.CurTime + component.UpdateInterval;
        }

        private void OnComponentShutdown(EntityUid uid, FrontierZoneGeneratorComponent component, ComponentShutdown args)
        {
            foreach (var entity in component.TrackedEntities)
            {
                RemComp<FrontierZoneComponent>(entity);
                DisableAlert(entity);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var genQuery = AllEntityQuery<FrontierZoneGeneratorComponent>();
            while (genQuery.MoveNext(out var genUid, out var component))
            {
                if (_gameTiming.CurTime < component.NextUpdate)
                    continue;

                _trackedNewBuffer.Clear();
                var query = _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(Transform(genUid).Coordinates, component.Radius);
                foreach (var humanoidUid in query)
                {
                    if (!_mindSystem.TryGetMind(humanoidUid, out var mindId, out var mind))
                        continue;

                    if (!component.TrackedEntities.Contains(humanoidUid))
                    {
                        EnableAlert(humanoidUid);
                        AddComp<FrontierZoneComponent>(humanoidUid);
                    }

                    _trackedNewBuffer.Add(humanoidUid);
                }

                _trackedRemoveBuffer.Clear();
                foreach (var humanoidUid in component.TrackedEntities)
                {
                    if (!_trackedNewBuffer.Contains(humanoidUid))
                        _trackedRemoveBuffer.Add(humanoidUid);
                }

                foreach (var humanoidUid in _trackedRemoveBuffer)
                {
                    RemComp<FrontierZoneComponent>(humanoidUid);
                    DisableAlert(humanoidUid);
                }

                component.TrackedEntities.Clear();
                component.TrackedEntities.UnionWith(_trackedNewBuffer);
                component.NextUpdate = _gameTiming.CurTime + component.UpdateInterval;
            }
        }

        private void EnableAlert(EntityUid entity)
        {
            _alerts.ShowAlert(entity, Alert);
        }

        private void DisableAlert(EntityUid entity)
        {
            _alerts.ClearAlert(entity, Alert);
        }
    }
}
