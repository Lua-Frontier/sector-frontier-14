using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Timing; // Lua

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed class ActionGunSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // Lua start

    private const float SuppressMainGunSeconds = 0.5f; // Lua end краткая задержка, чтобы не стрелять одновременно с навыком

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionGunComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ActionGunComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ActionGunComponent, ActionGunShootEvent>(OnShoot);
    }

    private void OnMapInit(Entity<ActionGunComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.Action))
            return;

        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        ent.Comp.Gun = Spawn(ent.Comp.GunProto);
    }

    private void OnShutdown(Entity<ActionGunComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Gun is {} gun)
            QueueDel(gun);
    }

    private void OnShoot(Entity<ActionGunComponent> ent, ref ActionGunShootEvent args)
    {
        if (TryComp<GunComponent>(ent.Comp.Gun, out var gun))
        {
            //  Lua Коротко заглушим основной ствол на владельце, чтобы навык не накладывался с очередным выстрелом
            if (TryComp<GunComponent>(ent.Owner, out var mainGun))
            {
                var until = _timing.CurTime + TimeSpan.FromSeconds(SuppressMainGunSeconds);
                if (mainGun.NextFire < until)
                {
                    mainGun.NextFire = until;
                    Dirty(ent.Owner, mainGun);
                }
            } // Lua end

            _gun.AttemptShoot(ent, ent.Comp.Gun.Value, gun, args.Target);
            args.Handled = true;  // Frontier: set handled
        }
    }
}

