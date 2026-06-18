// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Client.UserInterface;
using Content.Shared._Lua.Disease.UI;
using Content.Shared._Lua.Disease.Events;

namespace Content.Client._Lua.Disease.UI;

public sealed class DiseaseAnalyzerWindowBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DiseaseAnalyzerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DiseaseAnalyzerWindow>();

        _window.Analyze += OnAnalyze;
        _window.PrintReport += OnPrintReport;
        _window.Contain += OnContain;
        _window.ClearSample += OnClearSample;
    }

    private void OnAnalyze()
    {
        SendMessage(new DiseaseAnalyzerAnalyzeMessage());
    }

    private void OnPrintReport()
    {
        SendMessage(new DiseaseAnalyzerPrintReportMessage());
    }

    private void OnContain()
    {
        SendMessage(new DiseaseAnalyzerContainMessage());
    }

    private void OnClearSample()
    {
        SendMessage(new DiseaseAnalyzerClearSampleMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not DiseaseAnalyzerWindowInterfaceState cast)
        {
            return;
        }

        _window.UpdateState(cast);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window == null || message is not DiseaseAnalyzerAnalyzeTimerUpdate progressMessage)
        {
            return;
        }

        _window.UpdateProgress(progressMessage);
    }
}
