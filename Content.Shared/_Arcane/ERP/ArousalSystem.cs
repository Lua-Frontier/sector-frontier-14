using Content.Shared.Alert;
using Content.Shared._Lua.ERP;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Arcane.ERP;

public sealed class ArousalSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private static readonly TimeSpan PhaseCheckRate = TimeSpan.FromSeconds(5);

    private static readonly ProtoId<AlertCategoryPrototype> AlertCategory = "Arousal";
    private static readonly ProtoId<AlertPrototype> AlertAroused = "ArousalAroused";
    private static readonly ProtoId<AlertPrototype> AlertHeated = "ArousalHeated";
    private static readonly ProtoId<AlertCategoryPrototype> AlertCategoryRefractory = "ArousalRefractory";
    private static readonly ProtoId<AlertPrototype> AlertRefractory = "ArousalRefractory";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArousalComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<ArousalComponent> ent, ref ComponentInit args)
    {
        SetArousal(ent, 0f);
    }

    public float GetArousal(ArousalComponent comp)
    {
        var sinceChange = _timing.CurTime - comp.LastChangeTime;
        var elapsed = (float)sinceChange.TotalSeconds;
        var passiveRate = IsRefractory(comp) ? 0f : comp.PassiveGainRate;
        var netRate = passiveRate - comp.DecayRate;
        return Math.Clamp(comp.LastValue + netRate * elapsed, 0f, comp.MaxArousal);
    }

    public bool IsRefractory(ArousalComponent comp)
    {
        return _timing.CurTime < comp.RefractoryUntil;
    }

    public void SetPassiveSource(EntityUid uid, string sourceId, float rate, ArousalComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (IsErpDisabled(uid))
            return;

        comp.LastValue = GetArousal(comp);
        comp.LastChangeTime = _timing.CurTime;
        comp.PassiveSources[sourceId] = rate;
        Dirty(uid, comp);
    }

    public void RemovePassiveSource(EntityUid uid, string sourceId, ArousalComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.LastValue = GetArousal(comp);
        comp.LastChangeTime = _timing.CurTime;
        comp.PassiveSources.Remove(sourceId);
        Dirty(uid, comp);
    }

    public bool CanAddArousal(EntityUid uid, ArousalComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;
        if (IsErpDisabled(uid))
            return false;
        return !IsRefractory(comp);
    }

    public void AddArousal(EntityUid uid, float amount, ArousalComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (IsErpDisabled(uid))
            return;

        if (IsRefractory(comp))
            return;

        var before = GetArousal(comp);
        var target = Math.Clamp(before + amount, 0f, comp.MaxArousal);
        SetArousal((uid, comp), target);
        if (target > before)
            RaiseLocalEvent(uid, new ArousedEvent(before, target));
    }

    public void ReduceArousal(EntityUid uid, float amount, ArousalComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        SetArousal((uid, comp), GetArousal(comp) - amount);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ArousalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextPhaseCheckAt)
                continue;

            comp.NextPhaseCheckAt = now + PhaseCheckRate;
            UpdatePhase((uid, comp));
            UpdateRefractoryAlert(uid, comp);
        }
    }

    private void SetArousal(Entity<ArousalComponent> entity, float value)
    {
        var comp = entity.Comp;
        value = Math.Clamp(value, 0f, comp.MaxArousal);

        comp.LastValue = value;
        comp.LastChangeTime = _timing.CurTime;
        Dirty(entity.Owner, comp);

        UpdatePhase(entity);
    }

    private void UpdatePhase(Entity<ArousalComponent> entity)
    {
        var comp = entity.Comp;
        var newPhase = comp.ComputePhase(GetArousal(comp));
        if (newPhase == comp.CurrentPhase)
            return;

        var previous = comp.CurrentPhase;
        comp.CurrentPhase = newPhase;

        if (newPhase == ArousalPhase.Peak)
        {

            comp.LastOrgasmAt = _timing.CurTime;
            comp.RefractoryUntil = _timing.CurTime + comp.RefractoryDuration;
            comp.LastValue = 0f;
            comp.LastChangeTime = _timing.CurTime;
            comp.CurrentPhase = ArousalPhase.Calm;
            Dirty(entity.Owner, comp);

            UpdateAlerts(entity.Owner, ArousalPhase.Calm);
            _alerts.ShowAlert(entity.Owner, AlertRefractory);
            var orgasmEv = new ArousalOrgasmEvent();
            RaiseLocalEvent(entity.Owner, ref orgasmEv);
            RaiseLocalEvent(entity.Owner, new ArousalPhaseChangedEvent(previous, ArousalPhase.Calm));
        }
        else
        {
            Dirty(entity.Owner, comp);
            UpdateAlerts(entity.Owner, newPhase);
            RaiseLocalEvent(entity.Owner, new ArousalPhaseChangedEvent(previous, newPhase));
        }
    }

    private void UpdateAlerts(EntityUid uid, ArousalPhase phase)
    {
        switch (phase)
        {
            case ArousalPhase.Aroused:
                _alerts.ShowAlert(uid, AlertAroused);
                break;
            case ArousalPhase.Heated:
                _alerts.ShowAlert(uid, AlertHeated);
                break;
            default:
                _alerts.ClearAlertCategory(uid, AlertCategory);
                break;
        }
    }

    private void UpdateRefractoryAlert(EntityUid uid, ArousalComponent comp)
    {
        if (IsRefractory(comp))
            _alerts.ShowAlert(uid, AlertRefractory);
        else
            _alerts.ClearAlertCategory(uid, AlertCategoryRefractory);
    }

    private bool IsErpDisabled(EntityUid uid)
    {
        return !TryComp<ErpStatusComponent>(uid, out var status)
               || status.Status == EnumERPStatus.NO;
    }
}
