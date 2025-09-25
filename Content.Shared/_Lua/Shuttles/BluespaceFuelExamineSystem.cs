using Content.Shared._Mono.Ships;
using Content.Shared.Examine;

namespace Content.Shared._Lua.Shuttles;

public sealed class BluespaceFuelExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BluespaceFuelComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, BluespaceFuelComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange) return; 
        args.PushText($"Топливо: {comp.Count}/{(comp.MaxCount <= 0 ? 1 : comp.MaxCount)}");
    }
}


