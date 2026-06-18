// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Audio;
using Content.Shared.Paper;
using Content.Shared.Power;
using Content.Shared.Cargo;
using Content.Shared.Backmen.Disease;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._Lua.Disease.Components;
using Content.Shared._Lua.Disease.Events;
using Content.Shared._Lua.Disease.UI;
using Content.Server.Power.Components;

namespace Content.Server._Lua.Disease.Systems;

public sealed class DiseaseAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly LabelSystem _label = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly AudioSystem _sound = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseAnalyzerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, EntInsertedIntoContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<DiseaseAnalyzerComponent, EntRemovedFromContainerMessage>(OnItemSlotChanged);

        Subs.BuiEvents<DiseaseAnalyzerComponent>(DiseaseAnalyzerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUIOpen);
            subs.Event<DiseaseAnalyzerAnalyzeMessage>(OnAnalyzeButtonPressed);
            subs.Event<DiseaseAnalyzerContainMessage>(OnContainButtonPressed);
            subs.Event<DiseaseAnalyzerClearSampleMessage>(OnClearSampleButtonPressed);
            subs.Event<DiseaseAnalyzerPrintReportMessage>(OnPrintReportButtonPressed);
        });

        SubscribeLocalEvent<DiseaseContainerComponent, PriceCalculationEvent>(OnPriceCalculation);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiseaseAnalyzerComponent, ApcPowerReceiverComponent>();

        while (query.MoveNext(out var analyzer, out var analyzerComp, out var receiverComp))
        {
            var soundStream = analyzerComp.AnalyzingSoundStream;

            if (soundStream != null && analyzerComp.Status != DiseaseAnalyzerStatus.Analyzing)
            {
                analyzerComp.AnalyzingSoundStream = _sound.Stop(soundStream);
            }

            if (!receiverComp.Powered)
            {
                continue;
            }

            if (analyzerComp.ReportReloadTimer > 0f)
            {
                analyzerComp.ReportReloadTimer -= frameTime;
            }

            if (analyzerComp.AnalyzingTimer > 0f)
            {
                analyzerComp.AnalyzingTimer -= frameTime;
            }

            if (analyzerComp.Status == DiseaseAnalyzerStatus.Analyzing)
            {
                ProcessAnalyzing(analyzer, analyzerComp);
            }
        }
    }

    private void ProcessAnalyzing(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp)
    {
        if (!TrySampleComp(analyzer, analyzerComp, out var sampleComp))
        {
            _slots.SetLock(analyzer, analyzerComp.SampleSlotID, false);
            ResetStatus(analyzerComp);
            return;
        }

        if (analyzerComp.AnalyzingTimer > 0f)
        {
            UpdateUserTimer(analyzer, analyzerComp);
            return;
        }

        analyzerComp.Status = DiseaseAnalyzerStatus.Analyzed;
        analyzerComp.DiseaseIDs = sampleComp.DiseaseIDs;
        analyzerComp.AnalyzingSoundStream = _sound.Stop(analyzerComp.AnalyzingSoundStream);
        _sound.PlayPvs(analyzerComp.FinishSound, analyzer);
        _slots.SetLock(analyzer, analyzerComp.SampleSlotID, false);

        UpdateUserInterface(analyzer, analyzerComp);
    }

    private void OnUIOpen(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp, BoundUIOpenedEvent args)
    {
        UpdateUserInterface(analyzer, analyzerComp);
    }

    private void OnItemSlotChanged(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp, ContainerModifiedMessage args)
    {
        if (args.Container.ID != analyzerComp.SampleSlotID)
        {
            return;
        }

        _sound.PlayPvs(analyzerComp.InsertSound, analyzer);
        ResetStatus(analyzerComp);
        UpdateUserInterface(analyzer, analyzerComp);
    }

    private void OnPowerChanged(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp, ref PowerChangedEvent args)
    {
        if (analyzerComp.Status != DiseaseAnalyzerStatus.Analyzed)
        {
            ResetStatus(analyzerComp);
        }

        analyzerComp.AnalyzingSoundStream = _sound.Stop(analyzerComp.AnalyzingSoundStream);
        _slots.SetLock(analyzer, analyzerComp.SampleSlotID, false);
        UpdateUserInterface(analyzer, analyzerComp);
    }

    private void OnPrintReportButtonPressed(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp, DiseaseAnalyzerPrintReportMessage args)
    {
        if (analyzerComp.Status != DiseaseAnalyzerStatus.Analyzed
            || analyzerComp.ReportReloadTimer > 0f)
        {
            return;
        }

        CreateDiseaseReport(analyzerComp, Transform(analyzer).Coordinates);
        _sound.PlayPvs(analyzerComp.PrintSound, analyzer);
        analyzerComp.ReportReloadTimer = analyzerComp.ReportReloadTime;
        UpdateUserInterface(analyzer, analyzerComp);
    }

    private void CreateDiseaseReport(DiseaseAnalyzerComponent analyzerComp, EntityCoordinates coordinates)
    {
        var printed = Spawn(analyzerComp.ReportPrototype, coordinates);

        if (!TryComp<PaperComponent>(printed, out var paperComp))
        {
            QueueDel(printed);
            return;
        }

        var diseaseIDs = analyzerComp.DiseaseIDs;
        var contents = new FormattedMessage();

        if (diseaseIDs == null || diseaseIDs.Length == 0)
        {
            contents.TryAddMarkup(Loc.GetString("diagnoser-disease-report-none-contents"), out _);
            _paper.SetContent((printed, paperComp), contents.ToMarkup());
            return;
        }

        foreach (var diseaseID in diseaseIDs)
        {
            if (!_prototype.TryIndex(diseaseID, out var disease))
            {
                continue;
            }

            var diseaseName = Loc.GetString(disease.Name);
            contents.TryAddMarkup(Loc.GetString("diagnoser-disease-report-name", ("disease", diseaseName)), out _);
            contents.PushNewline();

            var infectiousString = disease.Infectious ? "diagnoser-disease-report-infectious" : "diagnoser-disease-report-not-infectious";
            contents.TryAddMarkup(Loc.GetString(infectiousString), out _);
            contents.PushNewline();

            var cureResistLine = disease.CureResist switch
            {
                < 0f => "diagnoser-disease-report-cureresist-none",
                <= 0.05f => "diagnoser-disease-report-cureresist-low",
                <= 0.14f => "diagnoser-disease-report-cureresist-medium",
                _ => "diagnoser-disease-report-cureresist-high"
            };

            contents.TryAddMarkup(Loc.GetString(cureResistLine), out _);
            contents.PushNewline();

            var cureExistsString = disease.Cures.Count == 0 ? "diagnoser-no-cures" : "diagnoser-cure-has";
            contents.TryAddMarkup(Loc.GetString(cureExistsString), out _);
            contents.PushNewline();

            foreach (var cure in disease.Cures)
            {
                contents.TryAddMarkup(cure.CureText(), out _);
                contents.PushNewline();
            }

            contents.PushNewline();
        }

        _label.Label(printed, GetDiseaseSumCode(diseaseIDs).ToString("X8"));
        _paper.SetContent((printed, paperComp), contents.ToMarkup());
    }

    private void OnClearSampleButtonPressed(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp, DiseaseAnalyzerClearSampleMessage args)
    {
        if (analyzerComp.Status == DiseaseAnalyzerStatus.Analyzing
            || !TrySampleComp(analyzer, analyzerComp, out var sampleComp))
        {
            return;
        }

        sampleComp.DiseaseIDs = null;

        if (sampleComp.Fragile)
        {
            QueueDel(_slots.GetItemOrNull(analyzer, analyzerComp.SampleSlotID));
        }

        _sound.PlayPvs(analyzerComp.ClearSound, analyzer);
        UpdateUserInterface(analyzer, analyzerComp);
    }

    private void OnAnalyzeButtonPressed(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp, DiseaseAnalyzerAnalyzeMessage args)
    {
        if (analyzerComp.Status != DiseaseAnalyzerStatus.NotAnalyzed
            || !TrySampleComp(analyzer, analyzerComp, out _))
        {
            return;
        }

        _slots.SetLock(analyzer, analyzerComp.SampleSlotID, true);
        analyzerComp.Status = DiseaseAnalyzerStatus.Analyzing;
        analyzerComp.AnalyzingTimer = analyzerComp.AnalyzingTime;
        analyzerComp.AnalyzingSoundStream = _sound.PlayPvs(analyzerComp.AnalyzingSound, analyzer)?.Entity;
        UpdateUserInterface(analyzer, analyzerComp);
    }

    private void OnContainButtonPressed(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp, DiseaseAnalyzerContainMessage args)
    {
        if (analyzerComp.Status == DiseaseAnalyzerStatus.Analyzing
            || !TrySampleComp(analyzer, analyzerComp, out var sampleComp)
            || !sampleComp.Fragile)
        {
            return;
        }

        var newContainer = Spawn(analyzerComp.DiseaseContainerPrototype, Transform(analyzer).Coordinates);

        if (!TryComp<DiseaseContainerComponent>(newContainer, out var newContainerComp))
        {
            QueueDel(newContainer);
            return;
        }

        newContainerComp.DiseaseIDs = sampleComp.DiseaseIDs;
        _label.Label(newContainer, GetDiseaseSumCode(sampleComp.DiseaseIDs).ToString("X8"));
        QueueDel(_slots.GetItemOrNull(analyzer, analyzerComp.SampleSlotID));
        UpdateUserInterface(analyzer, analyzerComp);
    }

    private static void ResetStatus(DiseaseAnalyzerComponent analyzerComp)
    {
        analyzerComp.Status = DiseaseAnalyzerStatus.NotAnalyzed;
        analyzerComp.DiseaseIDs = null;
    }

    private void UpdateUserInterface(EntityUid analyzer, DiseaseAnalyzerComponent? analyzerComp = null)
    {
        if (!Resolve(analyzer, ref analyzerComp)
            || !_userInterface.HasUi(analyzer, DiseaseAnalyzerUiKey.Key))
        {
            return;
        }

        UpdateUserTimer(analyzer, analyzerComp);

        var filled = TrySampleComp(analyzer, analyzerComp, out var sampleComp);

        if (!filled)
        {
            analyzerComp.DiseaseIDs = null;
        }

        var fragile = sampleComp == null ? false : sampleComp.Fragile;
        var code = sampleComp == null ? 0 : GetDiseaseSumCode(sampleComp.DiseaseIDs);

        var state = new DiseaseAnalyzerWindowInterfaceState(analyzerComp.Status, analyzerComp.DiseaseIDs, code, fragile, filled);
        _userInterface.SetUiState(analyzer, DiseaseAnalyzerUiKey.Key, state);
    }

    private void UpdateUserTimer(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp)
    {
        var progressMessage = new DiseaseAnalyzerAnalyzeTimerUpdate(GetProgress(analyzerComp));
        _userInterface.ServerSendUiMessage(analyzer, DiseaseAnalyzerUiKey.Key, progressMessage);
    }

    private bool TrySampleComp(EntityUid analyzer, DiseaseAnalyzerComponent analyzerComp, [NotNullWhen(true)] out DiseaseContainerComponent? sampleComp)
    {
        var sample = _slots.GetItemOrNull(analyzer, analyzerComp.SampleSlotID);
        return TryComp(sample, out sampleComp);
    }

    private static int GetDiseaseSumCode(ProtoId<DiseasePrototype>[]? diseaseIDs)
    {
        var sum = 0;

        if (diseaseIDs == null || diseaseIDs.Length == 0)
        {
            return sum;
        }

        foreach (var diseaseID in diseaseIDs)
        {
            if (string.IsNullOrEmpty(diseaseID))
            {
                continue;
            }

            sum += diseaseID.GetHashCode();
        }

        return sum % 599 * 59;
    }

    private static float GetProgress(DiseaseAnalyzerComponent analyzerComp)
    {
        var timerLenght = analyzerComp.AnalyzingTime;

        if (timerLenght <= 0f)
        {
            return 0f;
        }

        var timerLeft = analyzerComp.AnalyzingTimer;

        var progress = analyzerComp.Status switch
        {
            DiseaseAnalyzerStatus.NotAnalyzed => 0f,
            DiseaseAnalyzerStatus.Analyzing => Math.Clamp(1 - timerLeft / timerLenght, 0f, 1f),
            DiseaseAnalyzerStatus.Analyzed => 1f,
            _ => 0f
        };

        return progress;
    }

    private void OnPriceCalculation(EntityUid container, DiseaseContainerComponent containerComp, ref PriceCalculationEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        if (containerComp.DiseaseIDs == null || containerComp.DiseaseIDs.Length == 0)
        {
            return;
        }

        var price = 0;

        foreach (var diseaseID in containerComp.DiseaseIDs)
        {
            if (_prototype.TryIndex(diseaseID, out var disease))
            {
                price += disease.Price;
            }
        }

        args.Price += price;
        args.Handled = true;
    }
}
