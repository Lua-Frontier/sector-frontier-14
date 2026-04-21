// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Mono.FireControl;
using Content.Server.Administration;
using Content.Server.Administration.Components;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Cargo.Systems;
using Content.Shared._Mono.ShipGuns;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Administration;
using Content.Shared.Cargo.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Warps;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using System.Linq;
using System.Text;

namespace Content.Server._Lua.Shipyard.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class ShipStatCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public string Command => "shipstat";
    public string Description => "Показывает статистику шаттла текущего грида: размер, правила.";
    public string Help => $"{Command} — запустите находясь на гриде шаттла";

    private static int GetProcessingPowerCost(ShipGunClassComponent classComp)
    {
        if (classComp.ProcessingPowerCost is { } custom)
            return custom;

        return classComp.Class switch
        {
            ShipGunClass.Superlight => 1,
            ShipGunClass.Light => 3,
            ShipGunClass.Medium => 6,
            ShipGunClass.Heavy => 9,
            ShipGunClass.Superheavy => 12,
            _ => 0,
        };
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } attached)
        {
            shell.WriteLine("Должна выполняться на сервере и на гриде.");
            return;
        }

        var xform = _entities.GetComponent<TransformComponent>(attached);
        if (xform.GridUid == null)
        {
            shell.WriteLine("Вы не стоите на гриде шаттла.");
            return;
        }

        var gridUid = xform.GridUid.Value;

        if (!_entities.TryGetComponent<MapGridComponent>(gridUid, out var gridComp))
        {
            shell.WriteLine("Текущая сетка не является MapGrid.");
            return;
        }
        var mapUid = _entities.GetComponent<TransformComponent>(gridUid).MapUid;
        bool mapPaused = mapUid != null && _entities.TryGetComponent<MapComponent>(mapUid.Value, out var mapComp) && mapComp.MapPaused;

        var sb = new StringBuilder();
        sb.AppendLine("=== SHIPSTAT ===");
        var mapSystem = _systems.GetEntitySystem<MapSystem>();
        var tiles = mapSystem.GetAllTiles(gridUid, gridComp).ToList();
        int tileCount = tiles.Count;
        int width = 0, height = 0, maxSide = 0;

        if (tileCount > 0)
        {
            var minX = tiles.Min(t => t.X);
            var maxX = tiles.Max(t => t.X);
            var minY = tiles.Min(t => t.Y);
            var maxY = tiles.Max(t => t.Y);
            width = maxX - minX + 1;
            height = maxY - minY + 1;
            maxSide = Math.Max(width, height);
        }

        sb.AppendLine($"[Размер] {width}×{height}, тайлов: {tileCount}, макс сторона: {maxSide}");
        if (tileCount > 1412)
            sb.AppendLine($"  [!] Тайлов {tileCount} > макс Large (1412) — шаттл слишком большой");

        if (mapPaused)
        {
            sb.AppendLine("[Оценка] Карта заморожена — разморозьте карту для оценки стоимости.");
            sb.AppendLine("[Правила] Карта заморожена — разморозьте карту для проверки правил.");
            sb.AppendLine("=== END SHIPSTAT ===");
            shell.WriteLine(sb.ToString());
            return;
        }

        var size =
            tileCount > 961 ? VesselSize.Large :
            tileCount > 441 ? VesselSize.Medium :
            tileCount > 81 ? VesselSize.Small :
            VesselSize.Micro;
        sb.AppendLine($"[Категория по размеру] {size}  (Micro ≤13×13/100т, Small ≤21×21/441т, Medium ≤31×31/961т, Large ≤1412т)");
        var pricing = _systems.GetEntitySystem<PricingSystem>();
        double appraisePrice = 0;
        pricing.AppraiseGrid(gridUid, null, (_, price) => { appraisePrice += price; });
        var suggestedMinPrice = (int)(appraisePrice * 1.05f);
        var suggestedMaxPrice = (int)(appraisePrice * 1.3f);
        sb.AppendLine($"[Оценка] {appraisePrice:F0} cr - рекомендуемая цена: {suggestedMinPrice}–{suggestedMaxPrice} cr (наценка 5–30%)");
        sb.AppendLine("[Правила]");
        int totalGunCost = 0, gunCount = 0;
        var gunsQuery = _entities.EntityQueryEnumerator<ShipGunClassComponent, TransformComponent>();
        while (gunsQuery.MoveNext(out _, out var gunClass, out var gXform))
        {
            if (gXform.GridUid != gridUid) continue;
            totalGunCost += GetProcessingPowerCost(gunClass);
            gunCount++;
        }
        int totalServerCapacity = 0;
        var serverQuery = _entities.EntityQueryEnumerator<FireControlServerComponent, TransformComponent>();
        while (serverQuery.MoveNext(out _, out var serverComp, out var sXform))
        {
            if (sXform.GridUid != gridUid) continue;
            totalServerCapacity += serverComp.ProcessingPower;
        }
        sb.AppendLine($"  Орудий: {gunCount}, мощность: {totalGunCost}/{totalServerCapacity}  [{(gunCount == 0 || totalGunCost <= totalServerCapacity ? "OK" : "ПРЕВЫШЕНИЕ")}]");
        int airAlarms = 0;
        var aaQuery = _entities.EntityQueryEnumerator<AirAlarmComponent, TransformComponent>();
        while (aaQuery.MoveNext(out _, out var aXform))
        {
            if (aXform.GridUid == gridUid) airAlarms++;
        }
        sb.AppendLine($"  AirAlarm: {airAlarms}/2  [{(airAlarms <= 2 ? "OK" : "ПРЕВЫШЕНИЕ")}]");
        bool hasWarp = false;
        var warpQuery = _entities.EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
        while (warpQuery.MoveNext(out _, out var wXform))
        {
            if (wXform.GridUid == gridUid) { hasWarp = true; break; }
        }
        sb.AppendLine($"  WarpPoint: {(hasWarp ? "есть [OK]" : "ОТСУТСТВУЕТ [ОШИБКА]")}");
        int cashCount = 0;
        var cashQuery = _entities.EntityQueryEnumerator<CashComponent, TransformComponent>();
        while (cashQuery.MoveNext(out _, out var cXform))
        { if (cXform.GridUid == gridUid) cashCount++; }
        if (cashCount > 0) sb.AppendLine($"  Кредиты (CashComponent): {cashCount} [ОШИБКА — запрещено]");
        int godmodeCount = 0;
        var godQuery = _entities.EntityQueryEnumerator<GodmodeComponent, TransformComponent>();
        while (godQuery.MoveNext(out _, out var mXform))
        { if (mXform.GridUid == gridUid) godmodeCount++; }
        if (godmodeCount > 0) sb.AppendLine($"  GodmodeComponent: {godmodeCount} [ОШИБКА]");
        var forbiddenFound = new List<string>();
        string[] forbiddenPower = { "SMESBig", "ADTSMESIndustrial", "ADTSMESIndustrialEmpty", "DebugSMES" };
        string[] forbiddenGen = { "GeneratorWallmountAPU", "GeneratorWallmountBasic", "GeneratorRTG", "GeneratorRTGDamaged", "GeneratorBasic15kW", "DebugGenerator", "GeneratorBasic" };
        string[] indestructible = { "WallCultIndestructible", "WindowCultIndestructibleInvisible", "WallPlastitaniumDiagonalIndestructible", "WallPlastitaniumIndestructible", "PlastitaniumWindowIndestructible", "StationAnchorIndestructible" };
        string[] ftlBanned = { "MachineFTLDrive", "MachineFTLDrive50", "MachineFTLDrive25S" };
        string[] iffBanned = { "ComputerIFFSyndicateTypan", "ComputerIFFPOI", "ComputerTabletopIFFPOI", "ComputerIFFSyndicate", "ComputerTabletopIFFSyndicate" };
        int smesBasic = 0, smesAdvanced = 0, substationWallBasic = 0, substationBasic = 0;
        var metaQuery = _entities.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (metaQuery.MoveNext(out _, out var meta, out var mXform))
        {
            if (mXform.GridUid != gridUid) continue;
            var pid = meta.EntityPrototype?.ID;
            if (pid == null) continue;
            if (pid == "SubstationWallBasic") substationWallBasic++;
            if (pid == "SubstationBasic" || pid == "SubstationBasicEmpty") substationBasic++;
            if (pid == "SMESBasic" || pid == "SMESBasicEmpty") smesBasic++;
            if (pid == "SMESAdvanced" || pid == "SMESAdvancedEmpty") smesAdvanced++;
            if (forbiddenPower.Contains(pid)) forbiddenFound.Add($"[Энергия] {pid}");
            if (forbiddenGen.Contains(pid)) forbiddenFound.Add($"[Генератор] {pid}");
            if (indestructible.Contains(pid)) forbiddenFound.Add($"[Структура] {pid}");
            if (ftlBanned.Contains(pid)) forbiddenFound.Add($"[FTL] {pid}");
            if (iffBanned.Contains(pid)) forbiddenFound.Add($"[IFF] {pid}");
            if (pid == "ShieldGeneratorPOI") forbiddenFound.Add($"[Shield] {pid}");
            if (pid.Contains("GasMiner")) forbiddenFound.Add($"[Атмос] {pid}");
            if (pid.Contains("Debug")) forbiddenFound.Add($"[Debug] {pid}");
        }
        sb.AppendLine($"  SMES Basic: {smesBasic}  Advanced: {smesAdvanced}  SubstationWall: {substationWallBasic}  SubstationBasic: {substationBasic}");
        switch (size)
        {
            case VesselSize.Micro:
                if (substationWallBasic > 1) forbiddenFound.Add($"[Энергия] SubstationWallBasic: {substationWallBasic} > лимит 1 для Micro");
                if (smesBasic > 1) forbiddenFound.Add($"[Энергия] SMESBasic: {smesBasic} > лимит 1 для Micro");
                if (smesAdvanced > 0) forbiddenFound.Add($"[Энергия] SMESAdvanced запрещён для Micro");
                if (substationBasic > 0) forbiddenFound.Add($"[Энергия] SubstationBasic запрещён для Micro/Small/Medium");
                break;
            case VesselSize.Small:
                if (substationWallBasic > 2) forbiddenFound.Add($"[Энергия] SubstationWallBasic: {substationWallBasic} > лимит 2 для Small");
                if (smesBasic > 1) forbiddenFound.Add($"[Энергия] SMESBasic: {smesBasic} > лимит 1 для Small");
                if (smesAdvanced > 0) forbiddenFound.Add($"[Энергия] SMESAdvanced запрещён для Small");
                if (substationBasic > 0) forbiddenFound.Add($"[Энергия] SubstationBasic запрещён для Micro/Small/Medium");
                break;
            case VesselSize.Medium:
                if (substationWallBasic > 2) forbiddenFound.Add($"[Энергия] SubstationWallBasic: {substationWallBasic} > лимит 2 для Medium");
                if (smesBasic > 2) forbiddenFound.Add($"[Энергия] SMESBasic: {smesBasic} > лимит 2 для Medium");
                if (smesAdvanced > 0) forbiddenFound.Add($"[Энергия] SMESAdvanced запрещён для Medium");
                if (substationBasic > 0) forbiddenFound.Add($"[Энергия] SubstationBasic запрещён для Micro/Small/Medium");
                break;
            case VesselSize.Large:
                if (smesBasic > 0 && smesAdvanced > 0) forbiddenFound.Add($"[Энергия] нельзя смешивать SMESBasic и SMESAdvanced на Large");
                else if (smesBasic > 4) forbiddenFound.Add($"[Энергия] SMESBasic: {smesBasic} > лимит 4 для Large");
                else if (smesAdvanced > 4) forbiddenFound.Add($"[Энергия] SMESAdvanced: {smesAdvanced} > лимит 4 для Large");
                if (substationWallBasic > 0 && substationBasic > 0)
                {
                    if (substationWallBasic > 1) forbiddenFound.Add($"[Энергия] SubstationWallBasic: {substationWallBasic} > лимит 1 при смешивании на Large");
                    if (substationBasic > 2) forbiddenFound.Add($"[Энергия] SubstationBasic: {substationBasic} > лимит 2 при смешивании на Large");
                }
                else if (substationWallBasic > 3) forbiddenFound.Add($"[Энергия] SubstationWallBasic: {substationWallBasic} > лимит 3 для Large");
                else if (substationBasic > 2) forbiddenFound.Add($"[Энергия] SubstationBasic: {substationBasic} > лимит 2 для Large");
                break;
        }
        string[] allowedGunneryServers = size switch
        {
            VesselSize.Micro => ["GunneryServerLow"],
            VesselSize.Small => ["GunneryServerLow", "GunneryServerMedium"],
            VesselSize.Medium => ["GunneryServerLow", "GunneryServerMedium", "GunneryServerHigh"],
            VesselSize.Large => ["GunneryServerLow", "GunneryServerMedium", "GunneryServerHigh", "GunneryServerUltra"],
            _ => []
        };
        var gunneryQuery = _entities.EntityQueryEnumerator<FireControlServerComponent, MetaDataComponent, TransformComponent>();
        while (gunneryQuery.MoveNext(out _, out _, out var gMeta, out var gXform))
        {
            if (gXform.GridUid != gridUid) continue;
            var gPid = gMeta.EntityPrototype?.ID;
            if (gPid == null || !gPid.StartsWith("GunneryServer")) continue;
            if (!allowedGunneryServers.Contains(gPid))
                forbiddenFound.Add($"[Оружие] сервер '{gPid}' запрещён для {size}. Допустимы: {string.Join(", ", allowedGunneryServers)}");
        }

        if (forbiddenFound.Count > 0)
        {
            sb.AppendLine("  Нарушения:");
            foreach (var f in forbiddenFound.Distinct())
                sb.AppendLine($"    {f}");
        }
        else
        {
            sb.AppendLine("  Нарушений не найдено [OK]");
        }

        sb.AppendLine("=== END SHIPSTAT ===");
        shell.WriteLine(sb.ToString());
    }
}
