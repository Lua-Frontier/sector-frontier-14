using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Lua.Server.Medical.CritCrying;

public sealed partial class CritCryingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CritCryingComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<CritCryingComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical)
        {
            ent.Comp.NextCry = _timing.CurTime;
            return;
        }

        ent.Comp.NextCry = null;
        ent.Comp.Stream = _audio.Stop(ent.Comp.Stream);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CritCryingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextCry is not { } next || now < next)
                continue;

            comp.NextCry = now + _random.Next(comp.MinInterval, comp.MaxInterval);
            if (!_random.Prob(comp.Chance))
                continue;

            var sound = TryComp<HumanoidAppearanceComponent>(uid, out var humanoid) && humanoid.Sex == Sex.Female
                ? comp.FemaleSound
                : comp.MaleSound;
            comp.Stream = _audio.PlayPvs(sound, uid)?.Entity;
        }
    }
}
