using Content.Shared.Examine;
using Content.Shared.IdentityManagement;

namespace Content.Shared._Lua.Rotting;

public sealed class SharedFoodRottingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoodRottingComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, FoodRottingComponent component, ExaminedEvent args)
    {
        var stage = component.Stage;
        if (stage < 0 || stage > FoodRottingComponent.MaxStages) return;
        var key = $"foodrotting-{stage}";
        args.PushMarkup(Loc.GetString(key, ("target", Identity.Entity(uid, EntityManager))));
    }
}

