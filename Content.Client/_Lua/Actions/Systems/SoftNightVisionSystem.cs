// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Client.GameObjects;
using Content.Client._Lua.Actions.Components;
using Content.Shared._Lua.Actions.Components;
using Content.Shared.Coordinates;

namespace Content.Client._Lua.Actions.Systems;

public sealed class SoftNightVisionSystem : EntitySystem
{
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoftNightVisionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SoftNightVisionComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
    }

    public override void FrameUpdate(float frameTime)
    {
        var lightQuery = EntityQueryEnumerator<SoftNightVisionLightComponent, PointLightComponent>();

        while (lightQuery.MoveNext(out var light, out var comp, out var lightComp))
        {
            if (comp.Increase && comp.TimePassed > comp.TimeForFinalEnergy
                || !comp.Increase && comp.TimePassed < 0f)
            {
                continue;
            }

            comp.TimePassed += comp.Increase ? frameTime : -frameTime;
            var energy = comp.FinalEnergy * comp.TimePassed / comp.TimeForFinalEnergy;
            _light.SetEnergy(light, energy, lightComp);
        }
    }

    private void OnShutdown(EntityUid entity, SoftNightVisionComponent comp, ComponentShutdown args)
    {
        if (Exists(comp.InnerLight) || Exists(comp.OuterLight))
        {
            QueueDel(comp.InnerLight);
            QueueDel(comp.OuterLight);
        }
    }

    private void OnAfterHandleState(EntityUid entity, SoftNightVisionComponent comp, ref AfterAutoHandleStateEvent args)
    {
        comp.InnerLight = ToggleLight(entity, comp.Enable, comp.InnerLight, comp.InnerRadius, comp.InnerEnergy, comp.InnerSoftness, comp.Color, comp.TimeForFinalEnergy);
        comp.OuterLight = ToggleLight(entity, comp.Enable, comp.OuterLight, comp.OuterRadius, comp.OuterEnergy, comp.OuterSoftness, comp.Color, comp.TimeForFinalEnergy);
    }

    private EntityUid ToggleLight(EntityUid entity, bool enable, EntityUid? lightEntity, float radius, float energy, float softness, Color color, float timeForFinalEnergy)
    {
        var light = lightEntity != null ? lightEntity.Value : Spawn();

        var comp = EnsureComp<SoftNightVisionLightComponent>(light);
        comp.Increase = enable;
        comp.FinalEnergy = energy;
        comp.TimeForFinalEnergy = timeForFinalEnergy;

        var lightComp = _light.EnsureLight(light);
        _light.SetRadius(light, radius, lightComp);
        _light.SetSoftness(light, softness, lightComp);
        _light.SetColor(light, color, lightComp);
        _light.SetCastShadows(light, false, lightComp);
        _transform.SetCoordinates(light, entity.ToCoordinates());

        return light;
    }
}
