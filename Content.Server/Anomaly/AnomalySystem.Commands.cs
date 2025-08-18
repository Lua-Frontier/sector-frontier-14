using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Anomaly.Components;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Server.Anomaly;

public sealed partial class AnomalySystem
{
    [Dependency] private readonly IConsoleHost _consoleHost = default!;

    public void InitializeCommands()
    {
        _consoleHost.RegisterCommand("pulseanomaly", Loc.GetString("anomaly-command-pulse"), "pulseanomaly <uid>",
            PulseAnomalyCommand,
            GetAnomalyCompletion);

        _consoleHost.RegisterCommand("supercriticalanomaly", Loc.GetString("anomaly-command-supercritical"), "supercriticalanomaly <uid>",
            SupercriticalAnomalyCommand,
            GetAnomalyCompletion);
    }

    [AdminCommand(AdminFlags.Fun)]
    private void PulseAnomalyCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 1)
            shell.WriteError("Argument length must be 1");

        if (!NetEntity.TryParse(args[0], out var uidNet) || !TryGetEntity(uidNet, out var uid))
            return;

        if (!TryComp<AnomalyComponent>(uid, out var anomaly))
            return;

        DoAnomalyPulse(uid.Value, anomaly);
    }

    [AdminCommand(AdminFlags.Fun)]
    private void SupercriticalAnomalyCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 1)
            shell.WriteError("Argument length must be 1");

        if (!NetEntity.TryParse(args[0], out var uidNet) || !TryGetEntity(uidNet, out var uid))
            return;

        if (!TryComp<AnomalyComponent>(uid, out var anomaly))
            return;

        StartSupercriticalEvent((uid.Value, anomaly));
    }

    private CompletionResult GetAnomalyCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var hintArg = args[0];
        var entities = EntityQueryEnumerator<AnomalyComponent>();

        var options = new List<CompletionOption>();
        while (entities.MoveNext(out var uid, out var _))
        {
            var uidString = EntityManager.GetNetEntity(uid).ToString();
            options.Add(new CompletionOption(uidString, ToPrettyString(uid)));
        }

        return CompletionResult.FromHintOptions(options, hintArg);
    }
}
