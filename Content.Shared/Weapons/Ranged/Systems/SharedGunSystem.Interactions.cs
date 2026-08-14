using Content.Shared.Actions;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared._RMC14.Wieldable.Components;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    /// <summary>
    /// UseDelay id for the hands cooldown ring when drawing / selecting a gun.
    /// </summary>
    public const string GunDrawDelayId = "GunDraw";

    /// <summary>
    /// Default draw delay when a gun is equipped or selected in hand.
    /// </summary>
    private const float GunDrawDelaySeconds = 0.75f;

    private void OnExamine(EntityUid uid, GunComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !component.ShowExamineText)
            return;

        using (args.PushGroup(nameof(GunComponent)))
        {
            // Emberfall - Add caliber info
            if (TryGetGunCaliber(uid, component, out var caliber))
            {
                args.PushMarkup(Loc.GetString("gun-examine-caliber",
                    ("color", FireRateExamineColor),
                    ("caliber", caliber)));
            }
            // End Emberfall

            args.PushMarkup(Loc.GetString("gun-selected-mode-examine", ("color", ModeExamineColor),
                ("mode", GetLocSelector(component.SelectedMode))));

            if (component.DamageModifier != 1f)
                args.PushMarkup(Loc.GetString("gun-damage-modifier-examine", ("color", FireRateExamineColor),
                    ("damage", $"{component.DamageModifier.ToString("#.##")}")));
            //args.PushMarkup(Loc.GetString("gun-fire-rate-examine", ("color", FireRateExamineColor), // Emberfall
            //    ("fireRate", $"{component.FireRateModified:0.0}"))); // Emberfall
        }
    }

    private string GetLocSelector(SelectiveFire mode)
    {
        return Loc.GetString($"gun-{mode.ToString()}");
    }

    private void OnAltVerb(EntityUid uid, GunComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract || args.Hands == null || component.SelectedMode == component.AvailableModes)
            return;

        var nextMode = GetNextMode(component);

        AlternativeVerb verb = new()
        {
            Act = () => SelectFire(uid, component, nextMode, args.User),
            Text = Loc.GetString("gun-selector-verb", ("mode", GetLocSelector(nextMode))),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/fold.svg.192dpi.png")),
        };

        args.Verbs.Add(verb);
    }

    private SelectiveFire GetNextMode(GunComponent component)
    {
        var modes = new List<SelectiveFire>();

        foreach (var mode in Enum.GetValues<SelectiveFire>())
        {
            if ((mode & component.AvailableModes) == 0x0)
                continue;

            modes.Add(mode);
        }

        var index = modes.IndexOf(component.SelectedMode);
        return modes[(index + 1) % modes.Count];
    }

    private void SelectFire(EntityUid uid, GunComponent component, SelectiveFire fire, EntityUid? user = null)
    {
        if (component.SelectedMode == fire)
            return;

        DebugTools.Assert((component.AvailableModes  & fire) != 0x0);
        component.SelectedMode = fire;

        if (!Paused(uid))
        {
            var curTime = Timing.CurTime;
            var cooldown = TimeSpan.FromSeconds(InteractNextFire);

            if (component.NextFire < curTime)
                component.NextFire = curTime + cooldown;
            else
                component.NextFire += cooldown;
        }

        Audio.PlayPredicted(component.SoundMode, uid, user);
        Popup(Loc.GetString("gun-selected-mode", ("mode", GetLocSelector(fire))), uid, user);
        DirtyField(uid, component, nameof(GunComponent.SelectedMode));
        DirtyField(uid, component, nameof(GunComponent.NextFire));
    }

    /// <summary>
    /// Cycles the gun's <see cref="SelectiveFire"/> to the next available one.
    /// </summary>
    public void CycleFire(EntityUid uid, GunComponent component, EntityUid? user = null)
    {
        // Noop
        if (component.SelectedMode == component.AvailableModes)
            return;

        DebugTools.Assert((component.AvailableModes & component.SelectedMode) == component.SelectedMode);
        var nextMode = GetNextMode(component);
        SelectFire(uid, component, nextMode, user);
    }

    // TODO: Actions need doing for guns anyway.
    private sealed partial class CycleModeEvent : InstantActionEvent
    {
        public SelectiveFire Mode = default;
    }

    private void OnCycleMode(EntityUid uid, GunComponent component, CycleModeEvent args)
    {
        SelectFire(uid, component, args.Mode, args.Performer);
    }

    private void OnGunEquipped(EntityUid uid, GunComponent component, GotEquippedHandEvent args)
    {
        ApplyGunDrawDelay(uid, component);
    }

    private void OnGunSelected(EntityUid uid, GunComponent component, HandSelectedEvent args)
    {
        if (Timing.ApplyingState)
             return;

        ApplyGunDrawDelay(uid, component);
    }

    /// <summary>
    /// Hands UseDelay ring + NextFire gate after drawing / selecting a gun.
    /// Uses <see cref="WieldDelayComponent.ModifiedDelay"/> when present.
    /// </summary>
    private void ApplyGunDrawDelay(EntityUid uid, GunComponent component)
    {
        var delaySeconds = GunDrawDelaySeconds;
        if (TryComp(uid, out WieldDelayComponent? wieldDelay))
            delaySeconds = (float)wieldDelay.ModifiedDelay.TotalSeconds;

        if (delaySeconds <= 0f)
            return;

        var delay = TimeSpan.FromSeconds(delaySeconds);
        _useDelay.SetLength(uid, delay, GunDrawDelayId);
        _useDelay.TryResetDelay(uid, id: GunDrawDelayId);

        if (Paused(uid) || !component.ResetOnHandSelected)
            return;

        var curTime = Timing.CurTime;
        var minimum = curTime + delay;
        if (minimum <= component.NextFire)
            return;

        component.NextFire = minimum;
        DirtyField(uid, component, nameof(GunComponent.NextFire));
    }

    private void OnGunDrawShotAttempt(EntityUid uid, GunComponent component, ref ShotAttemptedEvent args)
    {
        if (!TryComp(uid, out UseDelayComponent? useDelay))
            return;

        if (_useDelay.IsDelayed((uid, useDelay), GunDrawDelayId))
        {
            args.Cancel();
            return;
        }

        // WieldDelay uses its own id; block here too so one path covers both.
        if (HasComp<WieldDelayComponent>(uid) &&
            _useDelay.IsDelayed((uid, useDelay), Content.Shared._RMC14.Wieldable.RMCWieldableSystem.WieldUseDelayId))
        {
            args.Cancel();
        }
    }
}
