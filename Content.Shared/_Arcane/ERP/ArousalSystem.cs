using Content.Shared._Arcane.ErpPanel; // Lua
using Content.Shared.Alert;
using Content.Shared.ERP;
using Robust.Shared.GameStates; // Lua
using Robust.Shared.Network;
using Robust.Shared.Player; // Lua
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
        SubscribeLocalEvent<ArousalComponent, ComponentGetStateAttemptEvent>(OnGetStateAttempt); // Lua
    }

    private void OnInit(Entity<ArousalComponent> ent, ref ComponentInit args)
    {
        SetArousal(ent, 0f);
    }

    // Lua start: only the owner and whoever has the panel open on them
    private void OnGetStateAttempt(Entity<ArousalComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var viewer = args.Player?.AttachedEntity;
        if (viewer == ent.Owner)
            return;

        if (TryComp<ErpPanelOwnerComponent>(viewer, out var panel) && panel.Target == ent.Owner)
            return;

        args.Cancelled = true;
    }
    // Lua end

    public float GetArousal(ArousalComponent comp)
    {
        var sinceChange = _timing.CurTime - comp.LastChangeTime;
        var elapsed = (float)sinceChange.TotalSeconds;
        return Math.Clamp(comp.LastValue - comp.DecayRate * elapsed, 0f, comp.MaxArousal); // Lua
    }

    public bool IsRefractory(ArousalComponent comp)
    {
        return _timing.CurTime < comp.RefractoryUntil;
    }

    public void AddArousal(EntityUid uid, float amount, ArousalComponent? comp = null, Filter? privateFilter = null) // Lua
    {
        if (!Resolve(uid, ref comp))
            return;

        if (IsErpDisabled(uid))
            return;

        if (IsRefractory(comp))
            return;

        SetArousal((uid, comp), GetArousal(comp) + amount, privateFilter); // Lua
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

    private void SetArousal(Entity<ArousalComponent> entity, float value, Filter? privateFilter = null) // Lua
    {
        var comp = entity.Comp;
        value = Math.Clamp(value, 0f, comp.MaxArousal);

        comp.LastValue = value;
        comp.LastChangeTime = _timing.CurTime;
        Dirty(entity.Owner, comp);

        UpdatePhase(entity, privateFilter); // Lua
    }

    private void UpdatePhase(Entity<ArousalComponent> entity, Filter? privateFilter = null) // Lua
    {
        var comp = entity.Comp;
        var newPhase = comp.ComputePhase(GetArousal(comp));
        if (newPhase == comp.CurrentPhase)
            return;

        comp.CurrentPhase = newPhase;

        if (newPhase == ArousalPhase.Peak)
        {
            comp.RefractoryUntil = _timing.CurTime + comp.RefractoryDuration;
            comp.LastValue = 0f;
            comp.LastChangeTime = _timing.CurTime;
            comp.CurrentPhase = ArousalPhase.Calm;
            Dirty(entity.Owner, comp);

            UpdateAlerts(entity.Owner, ArousalPhase.Calm);
            _alerts.ShowAlert(entity.Owner, AlertRefractory);
            var orgasmEv = new ArousalOrgasmEvent(privateFilter); // Lua
            RaiseLocalEvent(entity.Owner, ref orgasmEv);
        }
        else
        {
            Dirty(entity.Owner, comp);
            UpdateAlerts(entity.Owner, newPhase);
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
