using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Configuration;
using Content.Shared.Lua.CLVar;

namespace Content.Server._Lua.FTLPoints.Systems;

public sealed class StarmapConfigCommandSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        try
        {
            var consoleHost = IoCManager.Resolve<IConsoleHost>();
            if (consoleHost.AvailableCommands.ContainsKey("starmapconfig"))
            { Log.Warning("Command 'starmapconfig' already exists, skipping registration"); return; }
            try
            {
                consoleHost.RegisterCommand(new StarmapConfigCommand());
                Log.Info("StarmapConfigCommand registered successfully");
            }
            catch (ArgumentException ex) when (ex.Message.Contains("already been added"))
            { Log.Warning("Command 'starmapconfig' already exists, skipping registration"); }
            catch (Exception ex)
            { Log.Error($"Unexpected error registering StarmapConfigCommand: {ex}"); }
        }
        catch (Exception ex)
        { Log.Error($"Failed to resolve IConsoleHost: {ex}"); }
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class StarmapConfigCommand : IConsoleCommand
{
    public string Command => "starmapconfig";
    public string Description => "Manages Starmap configuration settings";
    public string Help => "Usage: starmapconfig <list|set|updatestarmap|checksectors> [setting] [value]\n" +
                         "  list - Show all current Starmap settings\n" +
                         "  set <setting> <value> - Set a specific setting\n" +
                         "  updatestarmap - Manually trigger a StarMap update\n" +
                         "  checksectors - Check the ready status of all sectors\n" +
                         "  Available settings:\n" +
                         "    min_stars - Minimum number of stars to generate\n" +
                         "    max_stars - Maximum number of stars to generate\n" +
                         "    include_sectors - Whether to include sector stars (true/false)\n" +
                         "    generate_roundstart - Whether to generate stars at round start (true/false)";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        { shell.WriteLine(Help); return; }
        switch (args[0].ToLower())
        {
            case "list": ListSettings(shell); break;
            case "set":
                if (args.Length != 3) { shell.WriteLine("Usage: starmapconfig set <setting> <value>"); return; }
                break;
            case "updatestarmap": UpdateStarMap(shell); break;
            case "checksectors": CheckSectors(shell); break;
            default: shell.WriteLine(Help); break;
        }
    }

    private void ListSettings(IConsoleShell shell)
    {
        var configManager = IoCManager.Resolve<IConfigurationManager>();
        shell.WriteLine("=== Starmap Configuration ===");
        shell.WriteLine($"Minimum stars: {configManager.GetCVar(CLVars.StarmapMinStars)}");
        shell.WriteLine($"Maximum stars: {configManager.GetCVar(CLVars.StarmapMaxStars)}");
        shell.WriteLine($"Include sectors: {configManager.GetCVar(CLVars.StarmapIncludeSectors)}");
        shell.WriteLine($"Generate at round start: {configManager.GetCVar(CLVars.GenerateStarmapRoundstart)}");
        shell.WriteLine("\nTo change settings, use: starmapconfig set <setting> <value>");
        shell.WriteLine("Example: starmapconfig set min_stars 5");
    }

    private void SetSetting(IConsoleShell shell, string setting, string value)
    {
        var configManager = IoCManager.Resolve<IConfigurationManager>();
        switch (setting.ToLower())
        {
            case "min_stars":
                if (int.TryParse(value, out var minStars))
                {
                    configManager.SetCVar(CLVars.StarmapMinStars, minStars);
                    shell.WriteLine($"Set minimum stars to {minStars}");
                }
                else
                { shell.WriteLine("Invalid value. Use a number."); }
                break;
            case "max_stars":
                if (int.TryParse(value, out var maxStars))
                {
                    configManager.SetCVar(CLVars.StarmapMaxStars, maxStars);
                    shell.WriteLine($"Set maximum stars to {maxStars}");
                }
                else
                { shell.WriteLine("Invalid value. Use a number."); }
                break;
            case "include_sectors":
                if (bool.TryParse(value, out var includeSectors))
                {
                    configManager.SetCVar(CLVars.StarmapIncludeSectors, includeSectors);
                    shell.WriteLine($"Set include sectors to {includeSectors}");
                }
                else
                { shell.WriteLine("Invalid value. Use 'true' or 'false'."); }
                break;
            case "generate_roundstart":
                if (bool.TryParse(value, out var generateRoundstart))
                {
                    configManager.SetCVar(CLVars.GenerateStarmapRoundstart, generateRoundstart);
                    shell.WriteLine($"Set generate at round start to {generateRoundstart}");
                }
                else
                { shell.WriteLine("Invalid value. Use 'true' or 'false'."); }
                break;
            default:
                shell.WriteLine($"Unknown setting: {setting}");
                shell.WriteLine("Available settings: min_stars, max_stars, include_sectors, generate_roundstart");
                break;
        }
    }

    private void UpdateStarMap(IConsoleShell shell)
    {
        try
        {
            var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            if (entitySystemManager.TryGetEntitySystem<SectorStarMapSystem>(out var sectorStarMapSystem))
            {
                shell.WriteLine("Triggering manual StarMap update...");
                sectorStarMapSystem.TriggerStarMapUpdate();
                shell.WriteLine("StarMap update triggered successfully");
            }
            else
            { shell.WriteLine("Error: SectorStarMapSystem not found"); }
        }
        catch (Exception ex)
        { shell.WriteLine($"Error triggering StarMap update: {ex.Message}"); }
    }

    private void CheckSectors(IConsoleShell shell)
    {
        try
        {
            var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            if (entitySystemManager.TryGetEntitySystem<SectorStarMapSystem>(out var sectorStarMapSystem))
            {
                shell.WriteLine("=== Sector Status Check ===");
                var diagnosticInfo = sectorStarMapSystem.GetDiagnosticInfo();
                shell.WriteLine(diagnosticInfo);
            }
            else
            { shell.WriteLine("Error: SectorStarMapSystem not found"); }
        }
        catch (Exception ex)
        { shell.WriteLine($"Error checking sectors: {ex.Message}"); }
    }
}
