using Content.Server.Power.Components;
using Content.Shared.AlertLevel;
using Content.Shared.Power;
using Content.Server._NF.SectorServices;
using Content.Server.Shuttles.Events;
using Robust.Shared.Map;

namespace Content.Server.AlertLevel;

public sealed class AlertLevelDisplaySystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertChanged);
        SubscribeLocalEvent<AlertLevelDisplayComponent, ComponentInit>(OnDisplayInit);
        SubscribeLocalEvent<AlertLevelDisplayComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    private void OnAlertChanged(AlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<AlertLevelDisplayComponent, AppearanceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var appearance, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            _appearance.SetData(uid, AlertLevelDisplay.CurrentLevel, args.AlertLevel, appearance);
        }
    }

    private void OnDisplayInit(EntityUid uid, AlertLevelDisplayComponent alertLevelDisplay, ComponentInit args)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        if (!_sectorService.TryGetServiceEntity(uid, out var serviceUid))
            return;

        if (TryComp(serviceUid, out AlertLevelComponent? alert))
            _appearance.SetData(uid, AlertLevelDisplay.CurrentLevel, alert.CurrentLevel, appearance);
    }

    private void OnPowerChanged(EntityUid uid, AlertLevelDisplayComponent alertLevelDisplay, ref PowerChangedEvent args)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        _appearance.SetData(uid, AlertLevelDisplay.Powered, args.Powered, appearance);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        SyncGridDisplaysToLocalAlert(args.Entity);
    }

    public void SyncGridDisplaysToLocalAlert(EntityUid grid)
    {
        if (!TryComp(grid, out TransformComponent? xform) || xform.MapID == MapId.Nullspace)
            return;

        if (!_sectorService.TryGetServiceEntity(xform.MapID, out var service) ||
            !TryComp(service, out AlertLevelComponent? alert) ||
            string.IsNullOrEmpty(alert.CurrentLevel))
            return;

        var level = alert.CurrentLevel;
        var query = EntityQueryEnumerator<AlertLevelDisplayComponent, AppearanceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var appearance, out var displayXform))
        {
            if (displayXform.GridUid != grid)
                continue;

            _appearance.SetData(uid, AlertLevelDisplay.CurrentLevel, level, appearance);
        }
    }
}
