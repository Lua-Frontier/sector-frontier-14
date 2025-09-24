using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._Mono.Ships;
using Content.Client.Items;

namespace Content.Client._Lua.Shuttles;

public sealed class BluespaceFuelHudSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<BluespaceFuelComponent, GunSystem.AmmoCounterControlEvent>(OnCollect);
        SubscribeLocalEvent<BluespaceFuelComponent, GunSystem.UpdateAmmoCounterEvent>(OnUpdate);
        SubscribeLocalEvent<BluespaceFuelComponent, ItemStatusCollectMessage>(OnItemStatusCollect);
    }

    private void OnItemStatusCollect(EntityUid uid, BluespaceFuelComponent comp, ItemStatusCollectMessage args)
    {
        var ev = new GunSystem.AmmoCounterControlEvent();
        RaiseLocalEvent(uid, ev);
        if (ev.Control == null) ev.Control = new GunSystem.BoxesStatusControl();
        args.Controls.Add(ev.Control);
        var update = new GunSystem.UpdateAmmoCounterEvent { Control = ev.Control };
        RaiseLocalEvent(uid, update);
    }

    private void OnCollect(EntityUid uid, BluespaceFuelComponent comp, GunSystem.AmmoCounterControlEvent args)
    { args.Control ??= new GunSystem.BoxesStatusControl(); }

    private void OnUpdate(EntityUid uid, BluespaceFuelComponent comp, GunSystem.UpdateAmmoCounterEvent args)
    {
        if (args.Control is GunSystem.BoxesStatusControl control)
        { control.Update(comp.Count, Math.Max(1, comp.MaxCount)); }
    }
}


