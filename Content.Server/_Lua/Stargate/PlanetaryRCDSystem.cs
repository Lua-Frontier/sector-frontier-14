using Content.Shared._Lua.Stargate;
using Content.Server._Lua.Stargate.Components;

namespace Content.Server._Lua.Stargate;

public sealed class PlanetaryRCDSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AttemptPlanetaryRCDUseEvent>(OnAttemptPlanetaryRCDUse);
    }

    private void OnAttemptPlanetaryRCDUse(AttemptPlanetaryRCDUseEvent ev)
    {
        if (HasComp<StargateDestinationComponent>(ev.GridUid))
        {
            ev.Allowed = true;
            return;
        }
        var mapUid = _transform.GetParentUid(ev.GridUid);
        ev.Allowed = HasComp<StargateDestinationComponent>(mapUid);
    }
}
