using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Lua.FTLPoints.Systems;

public sealed class SectorDiagnosticCommandSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        try
        {
            var consoleHost = IoCManager.Resolve<IConsoleHost>();
            if (consoleHost.AvailableCommands.ContainsKey("sectordiag"))
            {
                Log.Warning("Command 'sectordiag' already exists, skipping registration");
                return;
            }
            try
            {
                consoleHost.RegisterCommand(new SectorDiagnosticCommand());
                Log.Info("SectorDiagnosticCommand registered successfully");
            }
            catch (ArgumentException ex) when (ex.Message.Contains("already been added"))
            { Log.Warning("Command 'sectordiag' already exists, skipping registration"); }
            catch (Exception ex)
            { Log.Error($"Unexpected error registering SectorDiagnosticCommand: {ex}"); }
        }
        catch (Exception ex)
        { Log.Error($"Failed to resolve IConsoleHost: {ex}"); }
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class SectorDiagnosticCommand : IConsoleCommand
{
    public string Command => "sectordiag";
    public string Description => "Shows diagnostic information about the sector system";
    public string Help => "Usage: sectordiag";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sectorStarMap = IoCManager.Resolve<SectorStarMapSystem>();
        var diagnosticInfo = sectorStarMap.GetDiagnosticInfo();
        shell.WriteLine(diagnosticInfo);
        shell.WriteLine("\nAttempting to force update StarMaps...");
        sectorStarMap.ForceUpdateAllStarMaps();
        shell.WriteLine("Force update completed. Check logs for details.");
    }
}
