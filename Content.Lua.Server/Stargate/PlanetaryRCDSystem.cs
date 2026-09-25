using Content.Lua.Shared.Stargate;
using Content.Shared.Stargate;

namespace Content.Lua.Server.Stargate;

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
