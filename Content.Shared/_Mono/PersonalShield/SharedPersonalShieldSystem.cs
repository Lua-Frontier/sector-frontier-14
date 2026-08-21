using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.PersonalShield;

public sealed partial class SharedPersonalShieldSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly EntityQuery<BatteryComponent> _batteryQuery = default!;
    [Dependency] private readonly EntityQuery<PersonalShieldComponent> _shieldQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PersonalShieldComponent, PersonalShieldActionEvent>(OnAction);
        SubscribeLocalEvent<PersonalShieldComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<PersonalShieldComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PersonalShieldComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<PersonalShieldComponent, GotEquippedEvent>(OnGotEquipped);
    }

    private void OnAction(Entity<PersonalShieldComponent> ent, ref PersonalShieldActionEvent ev)
    {
        if (_timing.ApplyingState)
            return;

        var shield = ent.Comp;

        if (!_inventory.TryGetContainingSlot(ent.Owner, out var slot)
            || slot.SlotFlags == SlotFlags.POCKET
            || !_inventory.TryGetContainingEntity(ent.Owner, out var wearer)
            || wearer != ev.Performer)
        {
            _popup.PopupClient(Loc.GetString("personal-shield-toggle-not-worn"), ent, ev.Performer);
            _audio.PlayPredicted(shield.SoundFail, ent, ev.Performer);
            ev.Handled = true;
            return;
        }

        if (!shield.Activated && (shield.Runtime.Offline > 0f || shield.Runtime.Shatter > 0f))
        {
            _popup.PopupClient(
                Loc.GetString("personal-shield-toggle-fractured",
                    ("seconds", (int) MathF.Ceiling(shield.Runtime.Offline))),
                ent,
                ev.Performer);
            _audio.PlayPredicted(shield.SoundFail, ent, ev.Performer);
            ev.Handled = true;
            return;
        }

        shield.Activated = !shield.Activated;

        if (shield.Activated)
            ShutdownOtherWorn(ev.Performer, ent.Owner, ev.Performer);

        Dirty(ent, shield);

        _audio.PlayPredicted(
            shield.Activated ? shield.SoundActivate : shield.SoundDeactivate,
            ent,
            ev.Performer);

        ev.Toggle = true;
        ev.Handled = true;
    }

    private void OnGotUnequipped(Entity<PersonalShieldComponent> ent, ref GotUnequippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        BeginShutdown(ent, args.Equipee, playSound: true, visualWearer: args.Equipee);
    }

    private void OnGotEquipped(Entity<PersonalShieldComponent> ent, ref GotEquippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.VisualWearer == null)
            return;

        ent.Comp.VisualWearer = null;
        Dirty(ent, ent.Comp);
    }

    private void OnDamageModify(Entity<PersonalShieldComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        var shield = ent.Comp;
        if (!shield.IsUp || shield.Runtime.Charge <= 0f)
            return;

        if (_inventory.TryGetContainingEntity(ent.Owner, out var wearer)
            && !IsExclusiveShield(wearer.Value, ent))
            return;

        var modified = DamageSpecifier.ApplyModifierSet(args.Args.Damage, shield.Shield.BlockDamageModifier);

        var incoming = modified.GetTotal().Float();
        if (incoming <= 0f)
            return;

        var soaked = MathF.Min(incoming, shield.Runtime.Charge);
        shield.Runtime.Charge -= soaked;

        args.Args.Damage *= (incoming - soaked) / incoming;

        if (shield.Runtime.Charge <= 0f)
            Fracture(ent);
    }

    private void OnExamined(Entity<PersonalShieldComponent> ent, ref ExaminedEvent args)
    {
        var shield = ent.Comp;
        string msg;

        if (shield.Runtime.Shatter > 0f)
            msg = Loc.GetString("personal-shield-examine-broken");
        else if (shield.Runtime.Offline > 0f)
            msg = Loc.GetString("personal-shield-examine-offline",
                ("seconds", (int)MathF.Ceiling(shield.Runtime.Offline)));
        else if (shield.IsUp)
            msg = Loc.GetString("personal-shield-examine-up",
                ("percent", (int)MathF.Round(shield.Runtime.Charge / MathF.Max(shield.Shield.MaxCharge, 1f) * 100f)));
        else if (shield.Runtime.Form > 0f)
            msg = Loc.GetString("personal-shield-examine-spinup",
                ("percent", (int)MathF.Round(shield.Runtime.Form * 100f)));
        else
            msg = Loc.GetString("personal-shield-examine-down");

        args.PushMarkup(msg);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<PersonalShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            var ent = (uid, shield);
            var before = shield.Runtime;
            var wasActivated = shield.Activated;
            var hadVisual = shield.VisualWearer;
            var cfg = shield.Shield;

            if (shield.Runtime.Shatter > 0f)
            {
                shield.Runtime.Shatter += frameTime / MathF.Max(shield.ShatterTime, 0.01f);
                if (shield.Runtime.Shatter >= 1f)
                {
                    shield.Runtime.Shatter = 0f;
                    shield.Runtime.Form = 0f;
                    shield.VisualWearer = null;
                }

                DirtyIfChanged(ent, before, wasActivated, hadVisual);
                continue;
            }

            if (shield.Runtime.Offline > 0f)
            {
                shield.Runtime.Offline = MathF.Max(shield.Runtime.Offline - frameTime, 0f);
                DirtyIfChanged(ent, before, wasActivated, hadVisual);
                continue;
            }

            if (shield.Activated
                && _inventory.TryGetContainingEntity(uid, out var wearer)
                && !IsExclusiveShield(wearer.Value, ent))
            {
                BeginShutdown(ent, wearer, playSound: true);
                DirtyIfChanged(ent, before, wasActivated, hadVisual);
                continue;
            }

            var running = shield.Activated && TryDrawPower(ent, frameTime);

            var step = frameTime / MathF.Max(cfg.SpinupTime, 0.01f);

            if (running)
            {
                shield.Runtime.Form = MathF.Min(shield.Runtime.Form + step, 1f);

                shield.Runtime.Charge = shield.Runtime.Form < 1f
                    ? cfg.MaxCharge * shield.Runtime.Form
                    : MathF.Min(shield.Runtime.Charge + cfg.RegenRate * frameTime, cfg.MaxCharge);
            }
            else if (shield.Runtime.Form >= 1f)
            {
                shield.Runtime.Shatter = float.Epsilon;
                shield.Runtime.Charge = 0f;
            }
            else if (shield.Runtime.Form > 0f)
            {
                shield.Runtime.Form = MathF.Max(shield.Runtime.Form - step, 0f);
                shield.Runtime.Charge = cfg.MaxCharge * shield.Runtime.Form;
            }

            if (shield.Activated && !running && shield.Runtime.Form <= 0f)
                shield.Activated = false;

            DirtyIfChanged(ent, before, wasActivated, hadVisual);
        }
    }

    private bool TryDrawPower(Entity<PersonalShieldComponent> ent, float frameTime)
    {
        if (ent.Comp.Shield.PowerDraw <= 0f || !_batteryQuery.HasComp(ent))
            return true;

        return _battery.TryUseCharge(ent, ent.Comp.Shield.PowerDraw * frameTime);
    }

    public void Fracture(Entity<PersonalShieldComponent> ent)
    {
        ent.Comp.Runtime.Shatter = float.Epsilon;
        ent.Comp.Runtime.Charge = 0f;
        ent.Comp.Runtime.Offline = ent.Comp.Shield.BreakCooldown;
        ent.Comp.Activated = false;
        Dirty(ent, ent.Comp);
    }

    private void BeginShutdown(
        Entity<PersonalShieldComponent> ent,
        EntityUid? soundUser = null,
        bool playSound = false,
        EntityUid? visualWearer = null)
    {
        var shield = ent.Comp;
        var hadField = shield.Activated || shield.Runtime.Form > 0f || shield.Runtime.Shatter > 0f;
        if (!hadField)
            return;

        shield.Activated = false;

        if (shield.Runtime.Form > 0f || shield.Runtime.Shatter > 0f)
        {
            if (shield.Runtime.Shatter <= 0f)
                shield.Runtime.Shatter = float.Epsilon;
            shield.Runtime.Charge = 0f;
            if (visualWearer != null)
                shield.VisualWearer = visualWearer;
        }
        else
        {
            shield.Runtime.Form = 0f;
            shield.Runtime.Charge = 0f;
            shield.Runtime.Shatter = 0f;
        }

        Dirty(ent, shield);

        if (playSound)
            _audio.PlayPredicted(shield.SoundDeactivate, ent, soundUser);
    }

    private void ShutdownOtherWorn(EntityUid wearer, EntityUid keep, EntityUid? soundUser)
    {
        var slots = _inventory.GetSlotEnumerator(wearer);
        while (slots.NextItem(out var item))
        {
            if (item == keep || !_shieldQuery.TryComp(item, out var other))
                continue;

            BeginShutdown((item, other), soundUser, playSound: true);
        }
    }

    private bool IsExclusiveShield(EntityUid wearer, Entity<PersonalShieldComponent> candidate)
    {
        var bestUid = candidate.Owner;
        var bestCharge = candidate.Comp.Shield.MaxCharge;

        var slots = _inventory.GetSlotEnumerator(wearer);
        while (slots.NextItem(out var item))
        {
            if (!_shieldQuery.TryComp(item, out var other))
                continue;

            if (!other.Activated && !other.IsUp)
                continue;

            if (other.Shield.MaxCharge > bestCharge
                || (MathHelper.CloseTo(other.Shield.MaxCharge, bestCharge) && item.Id < bestUid.Id))
            {
                bestUid = item;
                bestCharge = other.Shield.MaxCharge;
            }
        }

        return bestUid == candidate.Owner;
    }

    private void DirtyIfChanged(
        Entity<PersonalShieldComponent> ent,
        PersonalShieldRuntime before,
        bool wasActivated,
        EntityUid? hadVisual)
    {
        var now = ent.Comp.Runtime;
        if (wasActivated == ent.Comp.Activated
            && hadVisual == ent.Comp.VisualWearer
            && MathHelper.CloseTo(before.Form, now.Form)
            && MathHelper.CloseTo(before.Shatter, now.Shatter)
            && MathHelper.CloseTo(before.Charge, now.Charge)
            && MathHelper.CloseTo(before.Offline, now.Offline))
        {
            return;
        }

        Dirty(ent, ent.Comp);
    }
}
