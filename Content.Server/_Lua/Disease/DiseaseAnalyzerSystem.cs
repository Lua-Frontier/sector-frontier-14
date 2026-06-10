// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Shared.Paper;
using Content.Server.Power.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Content.Shared.UserInterface;
using Content.Shared.Power;
using Content.Shared._Lua.Disease.Components;
using Content.Shared._Lua.Disease.Events;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Content.Shared._Lua.Disease.UI;
using Robust.Shared.Timing;
using Content.Shared.Backmen.Disease;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Lua.Disease;

public sealed class DiseaseAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _soundSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseAnalyzerComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, ComponentRemove>(OnComponentRemove);

        SubscribeLocalEvent<DiseaseAnalyzerComponent, EntInsertedIntoContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, EntRemovedFromContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<DiseaseAnalyzerComponent, AfterActivatableUIOpenEvent>(OnToggleInterface);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, DiseaseAnalyzerAnalyzeMessage>(OnAnalyzeButtonPressed);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, DiseaseAnalyzerContainMessage>(OnContainButtonPressed);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, DiseaseAnalyzerClearSampleMessage>(OnClearSampleButtonPressed);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, DiseaseAnalyzerPrintReportMessage>(OnPrintReportButtonPressed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiseaseAnalyzerComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var comp, out var receiver))
        {
            UpdateAppearance(uid, comp);

            if (!receiver.Powered)
                continue;

            ProcessAnalyzing(uid, comp);
        }
    }

    private void ProcessAnalyzing(EntityUid uid, DiseaseAnalyzerComponent component)
    {
        if (DiseaseAnalyzerStatus.Analyzing != component.Status || component.AnalyzingTime <= 0)
        {
            return;
        }

        var sample = component.SampleContainerSlot.Item;

        if (sample == null)
        {
            _itemSlotsSystem.SetLock(uid, component.SampleContainerSlot, false);
            component.Status = DiseaseAnalyzerStatus.NotAnalyzed;
            component.DiseaseIDs = null;
            return;
        }

        if (component.AnalyzingStartTime + TimeSpan.FromSeconds(component.AnalyzingTime) > _timing.CurTime)
        {
            UpdateUserInterface(uid, component);
            return;
        }

        _soundSystem.PlayPvs(component.FinishSound, uid);
        component.Status = DiseaseAnalyzerStatus.Analyzed;
        component.DiseaseIDs = null;

        if (TryComp<DiseaseContainerComponent>(sample, out var sampleComp))
        {
            component.DiseaseIDs = sampleComp.DiseaseIDs;
        }

        _itemSlotsSystem.SetLock(uid, component.SampleContainerSlot, false);
        UpdateUserInterface(uid, component);
    }

    private void OnComponentInit(EntityUid uid, DiseaseAnalyzerComponent component, ComponentInit args)
    {
        component.AnalyzingStartTime = _timing.CurTime;
        component.ReportReloadStartTime = _timing.CurTime;
        _itemSlotsSystem.AddItemSlot(uid, "SampleContainer", component.SampleContainerSlot);
    }

    private void OnComponentRemove(EntityUid uid, DiseaseAnalyzerComponent component, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, component.SampleContainerSlot);
    }

    private void OnMapInit(EntityUid uid, DiseaseAnalyzerComponent component, MapInitEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnToggleInterface(EntityUid uid, DiseaseAnalyzerComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnItemSlotChanged(EntityUid uid, DiseaseAnalyzerComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != component.SampleContainerSlot.ID)
        {
            return;
        }

        _soundSystem.PlayPvs(component.InsertSound, uid);
        component.Status = DiseaseAnalyzerStatus.NotAnalyzed;
        component.DiseaseIDs = null;
        UpdateUserInterface(uid, component);
    }

    private void OnPowerChanged(EntityUid uid, DiseaseAnalyzerComponent component, ref PowerChangedEvent args)
    {
        component.Powered = args.Powered;

        if (component.Status != DiseaseAnalyzerStatus.Analyzed)
        {
            component.Status = DiseaseAnalyzerStatus.NotAnalyzed;
            component.DiseaseIDs = null;
        }

        _itemSlotsSystem.SetLock(uid, component.SampleContainerSlot, false);
        UpdateUserInterface(uid, component);
    }

    private void OnPrintReportButtonPressed(EntityUid uid, DiseaseAnalyzerComponent component, DiseaseAnalyzerPrintReportMessage args)
    {
        if (component.Status == DiseaseAnalyzerStatus.Analyzing)
        {
            return;
        }

        if (component.ReportReloadStartTime + TimeSpan.FromSeconds(component.ReportReloadTime) > _timing.CurTime)
        {
            return;
        }

        var repProto = component.ReportPrototype;

        if (repProto == null)
        {
            return;
        }

        CreateDiseaseReport(component.DiseaseIDs, repProto, Transform(uid).Coordinates);
        component.ReportReloadStartTime = _timing.CurTime;
        _soundSystem.PlayPvs(component.PrintSound, uid);
        UpdateUserInterface(uid, component);
    }

    private void CreateDiseaseReport(string[]? diseaseIDs, string reportProto, EntityCoordinates coordinates)
    {
        var printed = Spawn(reportProto, coordinates);

        if (!TryComp<PaperComponent>(printed, out _))
        {
            QueueDel(printed);
            return;
        }

        var reportTitle = Loc.GetString("diagnoser-analyzer-report");
        FormattedMessage contents = new();

        if (diseaseIDs == null)
        {
            contents.TryAddMarkup(Loc.GetString("diagnoser-disease-report-none-contents"), out _);
            _metaData.SetEntityName(printed, reportTitle);
            _paperSystem.SetContent((printed, EnsureComp<PaperComponent>(printed)), contents.ToMarkup());
            return;
        }

        foreach (var diseaseID in diseaseIDs)
        {
            if (!_prototypeManager.TryIndex<DiseasePrototype>(diseaseID, out var disease))
            {
                continue;
            }

            var diseaseName = Loc.GetString(disease.Name);
            contents.TryAddMarkup(Loc.GetString("diagnoser-disease-report-name", ("disease", diseaseName)), out _);
            contents.PushNewline();

            if (disease.Infectious)
            {
                contents.TryAddMarkup(Loc.GetString("diagnoser-disease-report-infectious"), out _);
                contents.PushNewline();
            }
            else
            {
                contents.TryAddMarkup(Loc.GetString("diagnoser-disease-report-not-infectious"), out _);
                contents.PushNewline();
            }

            var cureResistLine = disease.CureResist switch
            {
                < 0f => Loc.GetString("diagnoser-disease-report-cureresist-none"),
                <= 0.05f => Loc.GetString("diagnoser-disease-report-cureresist-low"),
                <= 0.14f => Loc.GetString("diagnoser-disease-report-cureresist-medium"),
                _ => Loc.GetString("diagnoser-disease-report-cureresist-high")
            };

            contents.TryAddMarkup(cureResistLine, out _);
            contents.PushNewline();

            if (disease.Cures.Count == 0)
            {
                contents.TryAddMarkup(Loc.GetString("diagnoser-no-cures"), out _);
                contents.PushNewline();
                continue;
            }

            contents.TryAddMarkup(Loc.GetString("diagnoser-cure-has"), out _);
            contents.PushNewline();

            foreach (var cure in disease.Cures)
            {
                contents.TryAddMarkup(cure.CureText(), out _);
                contents.PushNewline();
            }
            contents.PushNewline();
        }
        _metaData.SetEntityName(printed, reportTitle);
        _paperSystem.SetContent((printed, EnsureComp<PaperComponent>(printed)), contents.ToMarkup());
    }

    private void OnClearSampleButtonPressed(EntityUid uid, DiseaseAnalyzerComponent component, DiseaseAnalyzerClearSampleMessage args)
    {
        var sample = component.SampleContainerSlot.Item;

        if (sample == null
            || !TryComp<DiseaseContainerComponent>(sample, out var sampleComp))
        {
            return;
        }

        sampleComp.DiseaseIDs = null;

        if (sampleComp.IsFragile)
        {
            if (_itemSlotsSystem.TryEject(uid, component.SampleContainerSlot, null, out var ejected)
            && ejected != null)
            {
                QueueDel(ejected.Value);
            }
        }

        _soundSystem.PlayPvs(component.ClearSound, uid);
        UpdateUserInterface(uid, component);
    }

    private void OnAnalyzeButtonPressed(EntityUid uid, DiseaseAnalyzerComponent component, DiseaseAnalyzerAnalyzeMessage args)
    {
        if (component.Status != DiseaseAnalyzerStatus.NotAnalyzed)
        {
            return;
        }

        var sample = component.SampleContainerSlot.Item;

        if (sample == null)
        {
            return;
        }

        if (!TryComp<DiseaseContainerComponent>(sample, out var sampleComp))
        {
            return;
        }

        if (sampleComp == null)
        {
            return;
        }

        _itemSlotsSystem.SetLock(uid, component.SampleContainerSlot, true);
        _soundSystem.PlayPvs(component.AnalyzingSound, uid);
        component.AnalyzingStartTime = _timing.CurTime;
        component.Status = DiseaseAnalyzerStatus.Analyzing;
        UpdateUserInterface(uid, component);
    }

    private void OnContainButtonPressed(EntityUid uid, DiseaseAnalyzerComponent component, DiseaseAnalyzerContainMessage args)
    {
        if (!component.Powered
            || component.Status == DiseaseAnalyzerStatus.Analyzing)
        {
            return;
        }

        var sample = component.SampleContainerSlot.Item;

        if (sample == null
            || !TryComp<DiseaseContainerComponent>(sample, out var sampleComp)
            || sampleComp == null
            || !sampleComp.IsFragile)
        {
            return;
        }

        var newContainer = Spawn(component.DiseaseContainerPrototype, Transform(uid).Coordinates);

        if (!TryComp<DiseaseContainerComponent>(newContainer, out var newContainerComp)
            || newContainerComp == null)
        {
            QueueDel(newContainer);
            return;
        }

        newContainerComp.DiseaseIDs = sampleComp.DiseaseIDs;

        if (_itemSlotsSystem.TryEject(uid, component.SampleContainerSlot, null, out var ejected)
            && ejected != null)
        {
            QueueDel(ejected.Value);
        }

        UpdateUserInterface(uid, component);
    }

    private void UpdateAppearance(EntityUid uid, DiseaseAnalyzerComponent component)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
        {
            return;
        }

        _appearance.SetData(uid, DiseaseAnalyzerVisuals.IsOn, component.Powered, appearance);
    }

    private void UpdateUserInterface(EntityUid uid, DiseaseAnalyzerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            return;
        }

        if (!_userInterface.IsUiOpen(uid, DiseaseAnalyzerWindowUiKey.Key))
        {
            return;
        }

        if (!component.Powered)
        {
            _userInterface.CloseUi(uid, DiseaseAnalyzerWindowUiKey.Key);
            return;
        }

        var sample = component.SampleContainerSlot.Item;
        var isFilled = true;

        if (sample == null)
        {
            isFilled = false;
            if (component.DiseaseIDs != null)
            {
                component.DiseaseIDs = null;
            }
        }

        List<string> diseaseNames = new();

        if (component.DiseaseIDs != null)
        {
            foreach (var diseaseID in component.DiseaseIDs)
            {
                if (_prototypeManager.TryIndex<DiseasePrototype>(diseaseID, out var disease))
                {
                    diseaseNames.Add(disease.Name);
                }
            }
        }

        var sum = 0;
        var isFragile = false;

        if (TryComp<DiseaseContainerComponent>(sample, out var sampleComp))
        {
            isFragile = sampleComp.IsFragile;

            if (sampleComp.DiseaseIDs != null)
            {
                foreach (var diseaseID in sampleComp.DiseaseIDs)
                {
                    if (!string.IsNullOrEmpty(diseaseID))
                    {
                        sum += diseaseID.GetHashCode();
                    }
                }
            }
        }

        var progressPercent = 0f;

        if (component.Status == DiseaseAnalyzerStatus.Analyzing && component.AnalyzingTime > 0)
        {
            progressPercent = (float)((_timing.CurTime - component.AnalyzingStartTime).TotalSeconds / component.AnalyzingTime);
        }

        var code = (sum % 190 * 57).ToString("X8");
        var status = component.Status;
        var progress = progressPercent;
        var fragile = isFragile;
        var filled = isFilled;

        var state = new DiseaseAnalyzerWindowInterfaceState(
            status,
            progress,
            diseaseNames,
            code,
            fragile,
            filled);

        _userInterface.SetUiState(uid, DiseaseAnalyzerWindowUiKey.Key, state);
    }
}
