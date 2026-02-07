using Content.Server.Fluids.EntitySystems;
using Content.Shared._Lua.Rotting;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Rotting;

public sealed class FoodRottingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly PuddleSystem _puddles = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoodRottingComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, FoodRottingComponent comp, MapInitEvent args)
    {
        comp.NextUpdate = _timing.CurTime + comp.UpdateRate;
        UpdateStageAndColor(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FoodRottingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (_timing.CurTime < comp.NextUpdate) continue;
            comp.NextUpdate += comp.UpdateRate;
            if (!comp.ForceProgression && IsInAntiRotContainer(uid, xform))
            {
                UpdateStageAndColor(uid, comp);
                continue;
            }
            comp.Accumulator += comp.UpdateRate;
            var total = SumDurations(comp);
            if (total > TimeSpan.Zero && comp.Accumulator >= total)
            {
                var sol = new Solution();
                sol.MaxVolume = comp.PuddleAmount;
                sol.AddReagent(comp.PuddleReagent, comp.PuddleAmount);
                _puddles.TrySpillAt(xform.Coordinates, sol, out _, sound: comp.PuddleSound);
                QueueDel(uid);
                continue;
            }
            UpdateStageAndColor(uid, comp);
            Dirty(uid, comp);
        }
    }

    private bool IsInAntiRotContainer(EntityUid uid, TransformComponent xform)
    {
        if (!_containers.TryGetOuterContainer(uid, xform, out var container)) return false;
        return HasComp<AntiRottingContainerComponent>(container.Owner);
    }

    private void UpdateStageAndColor(EntityUid uid, FoodRottingComponent comp, AppearanceComponent? appearance = null)
    {
        var stage = CalculateStage(comp);
        if (stage != comp.Stage) comp.Stage = stage;
        if (!Resolve(uid, ref appearance, false)) return;
        var color = GetStageColor(comp);
        _appearance.SetData(uid, FoodRottingVisuals.Color, color, appearance);
    }

    private static int CalculateStage(FoodRottingComponent comp)
    {
        var total = SumDurations(comp);
        if (total <= TimeSpan.Zero || comp.Accumulator <= TimeSpan.Zero)
            return 0;

        var t = comp.Accumulator;
        var stage0 = GetStageDuration(comp, 0);
        if (t < stage0)
            return 0;

        var elapsed = t - stage0;
        var boundary = TimeSpan.Zero;
        for (var i = 1; i <= FoodRottingComponent.MaxStages; i++)
        {
            boundary += GetStageDuration(comp, i);
            if (elapsed < boundary)
                return i;
        }
        return FoodRottingComponent.MaxStages;
    }

    private static TimeSpan GetStageDuration(FoodRottingComponent comp, int index)
    {
        return index >= 0 && index < comp.StageDurations.Count
            ? comp.StageDurations[index]
            : TimeSpan.Zero;
    }

    private static TimeSpan SumDurations(FoodRottingComponent comp)
    {
        long ticks = 0;
        for (var i = 0; i < FoodRottingComponent.StageCount; i++)
            ticks += GetStageDuration(comp, i).Ticks;
        return TimeSpan.FromTicks(ticks);
    }

    private static Color GetStageColor(FoodRottingComponent comp)
    {
        if (comp.Stage <= 0) return comp.BaseColor;
        var idx = comp.Stage - 1;
        if (idx >= 0 && idx < comp.StageColors.Count) return comp.StageColors[idx];
        return comp.BaseColor;
    }
}

