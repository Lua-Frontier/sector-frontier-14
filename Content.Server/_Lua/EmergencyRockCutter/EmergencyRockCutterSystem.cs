using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared._Lua.EmergencyRockCutter;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Server._Lua.EmergencyRockCutter;

public sealed class EmergencyRockCutterSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmergencyRockCutterComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<EmergencyRockCutterComponent, EmergencyRockCutterDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<EmergencyRockCutterComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<GatherableComponent>(target, out var gatherable))
            return;

        if (_whitelist.IsWhitelistFailOrNull(gatherable.ToolWhitelist, ent))
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("emergency-rock-cutter-start"), target, args.User);

        if (!_net.IsServer)
            return;

        var effectProto = ent.Comp.Effect ?? "EffectRCDDeconstruct4";
        var effect = Spawn(effectProto, Transform(target).Coordinates);
        var ev = new EmergencyRockCutterDoAfterEvent(GetNetEntity(effect));

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, ev, ent, target: target, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            QueueDel(effect);
    }

    private void OnDoAfter(Entity<EmergencyRockCutterComponent> ent, ref EmergencyRockCutterDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            if (_net.IsServer && args.Effect is { } effectNet) QueueDel(GetEntity(effectNet));
            return;
        }

        if (args.Handled || args.Target is not { } target)
            return;

        if (!TryComp<GatherableComponent>(target, out _))
            return;

        args.Handled = true;
        _gatherable.Gather(target, args.User);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/deconstruct.ogg"), target);
    }
}
