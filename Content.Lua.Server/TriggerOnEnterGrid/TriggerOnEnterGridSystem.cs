// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Explosion.EntitySystems;

namespace Content.Lua.Server.TriggerOnEnterGrid;

public sealed class TriggerOnEnterGridSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TriggerOnEnterGridComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out var component, out var xform))
        {
            switch (component.ReadyToTrigger)
            {
                case true when xform.GridUid.HasValue:
                    Trigger(entity);
                    break;
                case false when !xform.GridUid.HasValue:
                    component.ReadyToTrigger = true;
                    break;
            }
        }
    }

    public bool Trigger(EntityUid trigger, EntityUid? user = null)
    {
        var triggerEvent = new TriggerEvent(trigger, user);
        EntityManager.EventBus.RaiseLocalEvent(trigger, triggerEvent, true);
        return triggerEvent.Handled;
    }
}
