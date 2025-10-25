using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Lua.ThermalCloack;
using Content.Shared.Actions;
using Content.Shared.Inventory.Events;
using Content.Server.Popups;

namespace Content.Server._Lua.ThermalCloack;

public sealed class ThermalCloakSystem : EntitySystem
{
    [Dependency] private readonly TemperatureSystem _temperatureSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThermalCloakComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ThermalCloakComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ThermalCloakComponent, ToggleThermalCloakEvent>(OnToggle);
        SubscribeLocalEvent<ThermalCloakComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ThermalCloakComponent, GotUnequippedEvent>(OnUnequipped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ThermalCloakComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled || comp.Wearer == null) continue;
            if (TryComp(comp.Wearer.Value, out TemperatureComponent? temp))
            {
                var targetKelvin = 273.15f + comp.TargetCelsius;
                if (temp.CurrentTemperature <= targetKelvin) { continue; }
                var delta = MathF.Min(comp.CoolingRateKps * frameTime, temp.CurrentTemperature - targetKelvin);
                _temperatureSystem.ForceChangeTemperature(comp.Wearer.Value, temp.CurrentTemperature - delta, temp);
            }
        }
    }

    private void OnMapInit(Entity<ThermalCloakComponent> ent, ref MapInitEvent args)
    { EnsureAction(ent); }

    private void OnEquipped(Entity<ThermalCloakComponent> ent, ref GotEquippedEvent args)
    {
        EnsureAction(ent);
        ent.Comp.Wearer = args.Equipee;
        Dirty(ent);
        if (ent.Comp.Enabled) ApplyTemperature(args.Equipee, ent.Comp, true);
    }

    private void OnUnequipped(Entity<ThermalCloakComponent> ent, ref GotUnequippedEvent args)
    {
        RemoveAction(ent);
        SetEnabled(ent, false);
        ent.Comp.Wearer = null;
        Dirty(ent);
    }

    private void OnToggle(Entity<ThermalCloakComponent> ent, ref ToggleThermalCloakEvent args)
    {
        SetEnabled(ent, !ent.Comp.Enabled);
        _popupSystem.PopupEntity(Loc.GetString("thermal-cloak-toggle"), args.Performer, args.Performer);
        args.Handled = true;
    }
    private void OnGetActions(Entity<ThermalCloakComponent> ent, ref GetItemActionsEvent args)
    { args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction); }

    private void EnsureAction(Entity<ThermalCloakComponent> ent)
    {
        var comp = ent.Comp;
        var uid = ent.Owner;
        var container = Get<ActionContainerSystem>();
        container.EnsureAction(uid, ref comp.ToggleActionEntity, comp.ToggleAction);
        Dirty(uid, comp);
    }

    private void RemoveAction(Entity<ThermalCloakComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.ToggleActionEntity != null)
        {
            QueueDel(comp.ToggleActionEntity.Value);
            comp.ToggleActionEntity = null;
            Dirty(ent.Owner, comp);
        }
    }

    private void SetEnabled(Entity<ThermalCloakComponent> ent, bool enabled)
    {
        if (ent.Comp.Enabled == enabled) return;
        ent.Comp.Enabled = enabled;
        Dirty(ent);
        if (ent.Comp.Wearer != null) ApplyTemperature(ent.Comp.Wearer.Value, ent.Comp, enabled);
    }

    private void ApplyTemperature(EntityUid wearer, ThermalCloakComponent comp, bool enabled)
    {
        if (TryComp(wearer, out TemperatureComponent? temp))
        {
            if (enabled)
            {
                var targetKelvin = 273.15f + comp.TargetCelsius;
                if (temp.CurrentTemperature > targetKelvin) _temperatureSystem.ForceChangeTemperature(wearer, temp.CurrentTemperature - 0.01f, temp);
            }
        }
    }
}


