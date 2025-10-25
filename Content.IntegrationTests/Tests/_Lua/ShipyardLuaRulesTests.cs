// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Administration.Components;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Power.Components;
using Content.Shared._Mono.ShipGuns;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Cargo.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Warps;
using Content.Shared.Power.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Content.IntegrationTests.Tests._Lua;

[TestFixture]
public sealed class ShipyardLuaRulesTests
{
    private static readonly Dictionary<ShipGunClass, string> WeaponClassRu = new()
    {
        { ShipGunClass.Superlight, "суперлёгкий(Superlight)" },
        { ShipGunClass.Light, "лёгкий(Light)" },
        { ShipGunClass.Medium, "средний(Medium)" },
        { ShipGunClass.Heavy, "тяжёлый(Heavy)" },
        { ShipGunClass.Superheavy, "сверхтяжёлый(Superheavy)" },
    };

    private static readonly Dictionary<VesselSize, string> VesselSizeRu = new()
    {
        { VesselSize.Micro, "микро(Micro)" },
        { VesselSize.Small, "малый(Small)" },
        { VesselSize.Medium, "средний(Medium)" },
        { VesselSize.Large, "большой(Large)" },
    };

    private static readonly Dictionary<ShipGunClass, int> ClassPoints = new()
    {
        { ShipGunClass.Superlight, 1 },
        { ShipGunClass.Light, 2 },
        { ShipGunClass.Medium, 4 },
        { ShipGunClass.Heavy, 8 },
        { ShipGunClass.Superheavy, 16 },
    };

    private static readonly Dictionary<VesselSize, int> PointsCap = new()
    {
        { VesselSize.Micro, 4 },
        { VesselSize.Small, 8 },
        { VesselSize.Medium, 24 },
        { VesselSize.Large, 56 },
    };

    private static readonly Dictionary<VesselSize, Dictionary<ShipGunClass, int>> ClassMax = new()
    {
        {
            VesselSize.Micro, new Dictionary<ShipGunClass, int>
            {
                { ShipGunClass.Superlight, 4 },
                { ShipGunClass.Light, 1 },
                { ShipGunClass.Medium, 0 },
                { ShipGunClass.Heavy, 0 },
                { ShipGunClass.Superheavy, 0 },
            }
        },
        {
            VesselSize.Small, new Dictionary<ShipGunClass, int>
            {
                { ShipGunClass.Superlight, 6 },
                { ShipGunClass.Light, 2 },
                { ShipGunClass.Medium, 1 },
                { ShipGunClass.Heavy, 0 },
                { ShipGunClass.Superheavy, 0 },
            }
        },
        {
            VesselSize.Medium, new Dictionary<ShipGunClass, int>
            {
                { ShipGunClass.Superlight, 24 },
                { ShipGunClass.Light, 12 },
                { ShipGunClass.Medium, 6 },
                { ShipGunClass.Heavy, 3 },
                { ShipGunClass.Superheavy, 0 },
            }
        },
        {
            VesselSize.Large, new Dictionary<ShipGunClass, int>
            {
                { ShipGunClass.Superlight, 56 },
                { ShipGunClass.Light, 28 },
                { ShipGunClass.Medium, 14 },
                { ShipGunClass.Heavy, 7 },
                { ShipGunClass.Superheavy, 3 },
            }
        },
    };

    private static readonly string[] ForbiddenPowerAllSizes =
    {
        "SMESBig",
        "ADTSMESIndustrial",
        "ADTSMESIndustrialEmpty",
        "DebugSMES",
    };

    private static readonly string[] ForbiddenGeneratorsAllSizes =
    {
        "GeneratorWallmountAPU",
        "GeneratorWallmountBasic",
        "GeneratorRTG",
        "GeneratorRTGDamaged",
        "GeneratorBasic15kW",
        "DebugGenerator",
        "GeneratorBasic",
    };

    private static readonly string[] ConditionallyAllowedPowerLargeOnly =
    {
        "SMESAdvanced",
        "SMESAdvancedEmpty",
    };

    private static readonly string[] SubstationsBannedAlways =
    {
        "DebugSubstation",
    };

    private static readonly string[] SubstationsBannedExceptLarge =
    {
        "SubstationBasicEmpty",
        "SubstationBasic",
    };

    private static readonly string[] IndestructibleBannedAll =
    {
        "WallCultIndestructible",
        "WindowCultIndestructibleInvisible",
        "WallPlastitaniumDiagonalIndestructible",
        "WallPlastitaniumIndestructible",
        "PlastitaniumWindowIndestructible",
        "StationAnchorIndestructible",
    };

    private static readonly string[] FtlBannedCivilianExpedition =
    {
        "MachineFTLDrive600",
        "MachineFTLDrive",
        "MachineFTLDrive50",
        "MachineFTLDrive25S",
        "MachineWarpDrive",
    };

    private static readonly string[] FtlBannedAll =
    {
        "MachineFTLDrive",
        "MachineFTLDrive50",
        "MachineFTLDrive25S",
    };

    private static readonly string[] IffBannedAll =
    {
        "ComputerIFFSyndicateTypan",
        "ComputerIFFPOI",
        "ComputerTabletopIFFPOI",
        "ComputerIFFSyndicate",
        "ComputerTabletopIFFSyndicate",
    };

    private static readonly string[] IffBannedCivilianExpedition =
    {
        "ComputerIFF",
        "ComputerTabletopIFF",
    };

    private static readonly string[] DebugPrototypeIds =
    {
        "DebugGenerator", "DebugConsumer", "DebugBatteryStorage", "DebugBatteryDischarger", "DebugSMES", "DebugSubstation", "DebugAPC", "DebugPowerReceiver",
        "DebugThruster", "DebugGyroscope", "DebugThrusterSecurity", "DebugGyroscopeSecurity", "DebugThrusterNfsd", "DebugGyroscopeNfsd",
        "DebugVIE10", "DebugVIE100", "DebugVIE200", "DebugVIEhealer10", "DebugVIEhealer200", "DebugItemShapeWeird",
        "DebugFrontierStation",
        "DebugHardBomb",
        "DebugListing", "DebugListing2", "DebugListing3", "DebugListing4", "DebugListing5", "DebugDollar",
    };

    private static readonly string[] WhitelistedVessels =
    {
        "CourierRed",
        "CourierBlue",
    };

    private static readonly string[] LuaTechThrusters =
    {
        "ThrusterLuaBuild",
        "ThrusterLua",
    };

    private static readonly string[] UniversalShuttleConsoles =
    {
        "ComputerShuttle",
        "ComputerTabletopShuttle",
    };

    private static readonly Dictionary<VesselClass, string[]> FactionShuttleConsoles = new()
    {
        { VesselClass.Civilian, new[] { "ComputerShuttleWithFrontierDisk", "ComputerTabletopShuttleWithFrontierDisk" } },
        { VesselClass.Expedition, new[] { "ComputerShuttleWithFrontierDisk", "ComputerTabletopShuttleWithFrontierDisk" } },
        { VesselClass.Nfsd, new[] { "ComputerShuttleWithFrontierDisk", "ComputerTabletopShuttleWithFrontierDisk" } },
        { VesselClass.Mercenary, new[] { "ComputerShuttleWithMercenaryDisk", "ComputerTabletopShuttleWithMercenaryDisk" } },
        { VesselClass.Syndicate, new[] { "ComputerShuttleWithNordfallDisk", "ComputerTabletopShuttleWithNordfallDisk" } },
        { VesselClass.Pirate, new[] { "ComputerShuttleWithPirateDisk", "ComputerTabletopShuttleWithPirateDisk" } },
    };

    private static readonly Dictionary<VesselClass, string[]> FactionDisks = new()
    {
        { VesselClass.Civilian, new[] { "CoordinatesDiskFrontier" } },
        { VesselClass.Expedition, new[] { "CoordinatesDiskFrontier" } },
        { VesselClass.Nfsd, new[] { "CoordinatesDiskFrontier" } },
        { VesselClass.Mercenary, new[] { "CoordinatesDiskMercenary" } },
        { VesselClass.Syndicate, new[] { "CoordinatesDiskNordfall" } },
        { VesselClass.Pirate, new[] { "CoordinatesDiskPirate" } },
    };

    private static readonly string[] ForbiddenDisksAll =
    { "CoordinatesDiskDEBUG", };

    [Test]
    public async Task CheckLuaShipWeaponAndInfrastructureLimits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var map = entManager.System<MapSystem>();
        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var vessel in protoManager.EnumeratePrototypes<VesselPrototype>())
                {
                    if (WhitelistedVessels.Contains(vessel.ID))
                        continue;

                    map.CreateMap(out var mapId);
                    bool mapLoaded = false;
                    Entity<MapGridComponent>? shuttle = null;
                    try
                    { mapLoaded = mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out shuttle); }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Не удалось загрузить шаттл {vessel} ({vessel.ShuttlePath}): TryLoadGrid выбросил исключение {ex}");
                        map.DeleteMap(mapId); continue;
                    }
                    Assert.That(mapLoaded, Is.True, $"Не удалось загрузить шаттл {vessel} ({vessel.ShuttlePath}): TryLoadGrid вернул false.");
                    Assert.That(shuttle.HasValue, Is.True);
                    Assert.That(entManager.HasComponent<MapGridComponent>(shuttle.Value), Is.True);
                    if (!mapLoaded || shuttle == null)
                    { map.DeleteMap(mapId); continue; }
                    var gridUid = shuttle.Value.Owner;
                    var sb = new StringBuilder();
                    var classCounts = new Dictionary<ShipGunClass, int>
                    {
                        { ShipGunClass.Superlight, 0 },
                        { ShipGunClass.Light, 0 },
                        { ShipGunClass.Medium, 0 },
                        { ShipGunClass.Heavy, 0 },
                        { ShipGunClass.Superheavy, 0 },
                    };
                    var gunsQuery = entManager.EntityQueryEnumerator<ShipGunClassComponent, TransformComponent>();
                    int points = 0;
                    while (gunsQuery.MoveNext(out var uid, out var gunClass, out var xform))
                    {
                        if (xform.GridUid != gridUid) continue;
                        classCounts[gunClass.Class]++;
                        points += ClassPoints[gunClass.Class];
                    }
                    var size = vessel.Category;
                    if (vessel.Classes == null || !vessel.Classes.Any()) { sb.AppendLine($"[Класс] {vessel.ID}: поле 'class' обязательно и должно содержать хотя бы одно значение."); }
                    if (vessel.Engines == null || !vessel.Engines.Any()) { sb.AppendLine($"[Двигатель] {vessel.ID}: поле 'engine' обязательно и должно содержать хотя бы одно значение."); }
                    if (!PointsCap.TryGetValue(size, out var cap)) cap = 0;
                    if (points > cap)
                    {
                        var sizeRu = VesselSizeRu.TryGetValue(size, out var s) ? s : size.ToString();
                        sb.AppendLine($"[Оружие] {vessel.ID}: очки вооружения {points} превышают лимит {cap} для размера корабля '{sizeRu}'.");
                    }
                    if (ClassMax.TryGetValue(size, out var perClass))
                    {
                        foreach (var (cls, cnt) in classCounts)
                        {
                            if (perClass.TryGetValue(cls, out var max) && cnt > max)
                            {
                                var sizeRu = VesselSizeRu.TryGetValue(size, out var s) ? s : size.ToString();
                                var clsRu = WeaponClassRu.TryGetValue(cls, out var c) ? c : cls.ToString();
                                sb.AppendLine($"[Оружие] {vessel.ID}: класс орудий '{clsRu}': количество {cnt} превышает максимум {max} для размера корабля '{sizeRu}'.");
                            }
                        }
                    }
                    int airAlarms = 0;
                    var aaQuery = entManager.EntityQueryEnumerator<AirAlarmComponent, TransformComponent>();
                    while (aaQuery.MoveNext(out _, out var aXform))
                    { if (aXform.GridUid == gridUid) airAlarms++; }
                    if (airAlarms > 2) sb.AppendLine($"[Атмос] {vessel.ID}: AirAlarm {airAlarms} максимум может быть 2.");
                    var metaQuery = entManager.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
                    var debugFound = new List<string>();
                    int substationWallBasic = 0;
                    int substationBasicTotal = 0;
                    int smesBasicTotal = 0;
                    int smesAdvancedTotal = 0;
                    while (metaQuery.MoveNext(out _, out var meta, out var mXform))
                    {
                        if (mXform.GridUid != gridUid) continue;
                        var pid = meta.EntityPrototype?.ID;
                        if (pid == null) continue;
                        var isLuaTech = (vessel.Marker == "LuaTech") || vessel.Name.Contains("LuaTech", StringComparison.OrdinalIgnoreCase);
                        if (pid == "SubstationWallBasic") substationWallBasic++;
                        if (pid == "SubstationBasic" || pid == "SubstationBasicEmpty") substationBasicTotal++;
                        if (pid == "SMESBasic" || pid == "SMESBasicEmpty") smesBasicTotal++;
                        if (pid == "SMESAdvanced" || pid == "SMESAdvancedEmpty") smesAdvancedTotal++;
                        if (pid.Contains("GasMiner", StringComparison.Ordinal)) sb.AppendLine($"[Атмос] {vessel.ID}: GasMiner '{pid}' запрещён.");
                        if (ForbiddenPowerAllSizes.Contains(pid)) sb.AppendLine($"[Энергия] {vessel.ID}: запрещённый прототип питания '{pid}'.");
                        if (ForbiddenGeneratorsAllSizes.Contains(pid)) sb.AppendLine($"[Генераторы] {vessel.ID}: запрещённый генератор '{pid}'.");
                        if (ConditionallyAllowedPowerLargeOnly.Contains(pid) && size != VesselSize.Large) sb.AppendLine($"[Энергия] {vessel.ID}: '{pid}' разрешён только на Large, текущий размер: {size}.");
                        if (SubstationsBannedAlways.Contains(pid))  sb.AppendLine($"[Энергия] {vessel.ID}: запрещённая подстанция '{pid}'.");
                        if (size != VesselSize.Large && SubstationsBannedExceptLarge.Contains(pid)) sb.AppendLine($"[Энергия] {vessel.ID}: подстанция '{pid}' запрещена для размера {size}.");
                        if (IndestructibleBannedAll.Contains(pid)) sb.AppendLine($"[Структуры] {vessel.ID}: запрещён неразрушимый объект '{pid}'.");
                        if (pid == "MachineAnomalyGenerator" && size != VesselSize.Large) sb.AppendLine($"[Аномалии] {vessel.ID}: 'MachineAnomalyGenerator' разрешён только на Large, текущий размер: {size}.");
                        if (pid == "CircularShieldBase" && size == VesselSize.Large) sb.AppendLine($"[Shield] {vessel.ID}: '{pid}' запрещён на Large.");
                        if (pid == "ShieldGeneratorPOI") { sb.AppendLine($"[Shield] {vessel.ID}: 'ShieldGeneratorPOI' запрещён на всех шаттлах."); }
                        if (pid == "ShieldGeneratorTSFCapital")
                        {
                            var allowed = size == VesselSize.Large && vessel.Classes != null && vessel.Classes.Contains(VesselClass.Nfsd);
                            if (!allowed) sb.AppendLine($"[Shield] {vessel.ID}: 'ShieldGeneratorTSFCapital' разрешён только для класса Nfsd и размера Large.");
                        }
                        if (pid == "ShieldGenerator")
                        {
                            var isLarge = size == VesselSize.Large;
                            var hasAllowedClass = vessel.Classes != null && (vessel.Classes.Contains(VesselClass.Nfsd) || vessel.Classes.Contains(VesselClass.Pirate) || vessel.Classes.Contains(VesselClass.Syndicate));
                            var isCivilianLarge = isLarge && vessel.Classes != null && vessel.Classes.Contains(VesselClass.Civilian);
                            if (isCivilianLarge) sb.AppendLine($"[Shield] {vessel.ID}: щиты запрещены на гражданских Large.");
                            else if (!hasAllowedClass) sb.AppendLine($"[Shield] {vessel.ID}: 'ShieldGenerator' на Large разрешён только для классов Nfsd/Pirate/Syndicate.");
                        }
                        if (pid == "ShieldGeneratorMedium")
                        {
                            var isCivilianLarge = size == VesselSize.Large && vessel.Classes != null && vessel.Classes.Contains(VesselClass.Civilian);
                            if (isCivilianLarge) sb.AppendLine($"[Shield] {vessel.ID}: щиты запрещены на гражданских Large.");
                            if (size == VesselSize.Small || size == VesselSize.Micro) sb.AppendLine($"[Shield] {vessel.ID}: 'ShieldGeneratorMedium' запрещён для размеров Small/Micro.");
                        }
                        if (pid == "ShieldGeneratorSmall")
                        {
                            var isCivilianLarge = size == VesselSize.Large && vessel.Classes != null && vessel.Classes.Contains(VesselClass.Civilian);
                            if (isCivilianLarge) sb.AppendLine($"[Shield] {vessel.ID}: щиты запрещены на гражданских Large.");
                        }
                        if ((pid == "CircularShieldLuaBuild" || pid == "CircularShieldLua") && !isLuaTech) sb.AppendLine($"[Щиты] {vessel.ID}: '{pid}' разрешён только для LuaTech шаттлов.");
                        if (LuaTechThrusters.Contains(pid) && !isLuaTech) sb.AppendLine($"[Двигатели] {vessel.ID}: '{pid}' разрешён только для LuaTech шаттлов.");
                        if (IffBannedAll.Contains(pid)) sb.AppendLine($"[IFF] {vessel.ID}: '{pid}' запрещён на всех шаттлах.");
                        if ((vessel.Classes != null && (vessel.Classes.Contains(VesselClass.Civilian) || vessel.Classes.Contains(VesselClass.Expedition))) && IffBannedCivilianExpedition.Contains(pid)) sb.AppendLine($"[IFF] {vessel.ID}: '{pid}' запрещён для Civilian/Expedition.");
                        if (pid.Contains("Debug", StringComparison.Ordinal) || DebugPrototypeIds.Contains(pid)) debugFound.Add(pid);
                        if (FtlBannedAll.Contains(pid)) { sb.AppendLine($"[FTL] {vessel.ID}: '{pid}' запрещён на всех шаттлах."); }
                        if ((vessel.Classes != null && (vessel.Classes.Contains(VesselClass.Civilian) || vessel.Classes.Contains(VesselClass.Expedition))) && FtlBannedCivilianExpedition.Contains(pid))
                        { sb.AppendLine($"[FTL] {vessel.ID}: '{pid}' запрещён для Civilian/Expedition."); }
                        var allFactionConsoles = FactionShuttleConsoles.SelectMany(kvp => kvp.Value).ToArray();
                        if (allFactionConsoles.Contains(pid) && !UniversalShuttleConsoles.Contains(pid))
                        {
                            bool consoleAllowed = false;
                            if (vessel.Classes != null)
                            {
                                foreach (var vesselClass in vessel.Classes)
                                { if (FactionShuttleConsoles.TryGetValue(vesselClass, out var allowedConsoles) && allowedConsoles.Contains(pid)) { consoleAllowed = true; break; } }
                            }
                            if (!consoleAllowed)
                            {
                                var factionName = FactionShuttleConsoles.FirstOrDefault(kvp => kvp.Value.Contains(pid)).Key.ToString();
                                var vesselClassesStr = vessel.Classes != null ? string.Join(", ", vessel.Classes) : "нет";
                                sb.AppendLine($"[Консоли] {vessel.ID}: консоль '{pid}' (фракция {factionName}) запрещена для классов шаттла [{vesselClassesStr}].");
                            }
                        }
                        var allFactionDisks = FactionDisks.SelectMany(kvp => kvp.Value).ToArray();
                        if (allFactionDisks.Contains(pid))
                        {
                            bool diskAllowed = false;
                            if (vessel.Classes != null)
                            {
                                foreach (var vesselClass in vessel.Classes)
                                { if (FactionDisks.TryGetValue(vesselClass, out var allowedDisks) && allowedDisks.Contains(pid)) { diskAllowed = true; break; } }
                            }
                            if (!diskAllowed)
                            {
                                var factionName = FactionDisks.FirstOrDefault(kvp => kvp.Value.Contains(pid)).Key.ToString();
                                var vesselClassesStr = vessel.Classes != null ? string.Join(", ", vessel.Classes) : "нет";
                                sb.AppendLine($"[Диски] {vessel.ID}: диск '{pid}' (фракция {factionName}) запрещён для классов шаттла [{vesselClassesStr}].");
                            }
                        }
                        if (ForbiddenDisksAll.Contains(pid))
                        { sb.AppendLine($"[Диски] {vessel.ID}: диск '{pid}' запрещён на всех шаттлах."); }
                    }
                    int godmodeCount = 0;
                    var godQuery = entManager.EntityQueryEnumerator<GodmodeComponent, TransformComponent>();
                    while (godQuery.MoveNext(out _, out var gXform)) { if (gXform.GridUid == gridUid) godmodeCount++; }
                    if (godmodeCount > 0) sb.AppendLine($"[Админ] {vessel.ID}: обнаружен компонент 'GodmodeComponent' на {godmodeCount} сущностях.");
                    int minigunCount = 0;
                    var minigunQuery = entManager.EntityQueryEnumerator<AdminMinigunComponent, TransformComponent>();
                    while (minigunQuery.MoveNext(out _, out var mXform)) { if (mXform.GridUid == gridUid) minigunCount++; }
                    if (minigunCount > 0) sb.AppendLine($"[Админ] {vessel.ID}: обнаружен 'AdminMinigunComponent' на {minigunCount} сущностях.");
                    int cashCount = 0;
                    var cashQuery = entManager.EntityQueryEnumerator<CashComponent, TransformComponent>();
                    while (cashQuery.MoveNext(out _, out var cXform)) { if (cXform.GridUid == gridUid) cashCount++; }
                    if (cashCount > 0) sb.AppendLine($"[Экономика] {vessel.ID}: обнаружены кредиты (CashComponent) на {cashCount} сущностях — запрещено на шаттлах.");
                    var sizeRuName = VesselSizeRu.TryGetValue(size, out var sr) ? sr : size.ToString();
                    switch (size)
                    {
                        case VesselSize.Micro:
                            if (substationWallBasic > 1) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} лимит 'SubstationWallBasic' - 1, обнаружено {substationWallBasic}.");
                            if (smesBasicTotal > 1) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} лимит 'SMESBasic/SMESBasicEmpty' - 1, обнаружено {smesBasicTotal}."); break;
                        case VesselSize.Small:
                            if (substationWallBasic > 2) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} лимит 'SubstationWallBasic' - 2, обнаружено {substationWallBasic}.");
                            if (smesBasicTotal > 1) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} лимит 'SMESBasic/SMESBasicEmpty' - 1, обнаружено {smesBasicTotal}."); break;
                        case VesselSize.Medium:
                            if (substationWallBasic > 2) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} лимит 'SubstationWallBasic' - 2, обнаружено {substationWallBasic}.");
                            if (smesBasicTotal > 2) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} лимит 'SMESBasic/SMESBasicEmpty' - 2, обнаружено {smesBasicTotal}."); break;
                        case VesselSize.Large:
                            if (substationWallBasic > 0 && substationBasicTotal > 0)
                            {
                                if (substationWallBasic > 1) sb.AppendLine($"[Энергия] {vessel.ID}: при смешивании подстанций на {sizeRuName} допустимо не более 1 'SubstationWallBasic'; обнаружено {substationWallBasic}.");
                                if (substationBasicTotal > 2) sb.AppendLine($"[Энергия] {vessel.ID}: при смешивании подстанций на {sizeRuName} допустимо не более 2 'SubstationBasic/SubstationBasicEmpty'; обнаружено {substationBasicTotal}.");
                            }
                            else if (substationWallBasic > 0)
                            { if (substationWallBasic > 3) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} допустимо не более 3 'SubstationWallBasic' без смешивания; обнаружено {substationWallBasic}."); }
                            else if (substationBasicTotal > 0)
                            { if (substationBasicTotal > 2) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} допустимо не более 2 'SubstationBasic/SubstationBasicEmpty' без смешивания; обнаружено {substationBasicTotal}."); }
                            if (smesBasicTotal > 0 && smesAdvancedTotal > 0)
                            { sb.AppendLine($"[Энергия] {vessel.ID}: на {sizeRuName} запрещено смешивать 'SMESBasic/SMESBasicEmpty' и 'SMESAdvanced/SMESAdvancedEmpty'."); }
                            else if (smesBasicTotal > 0)
                            { if (smesBasicTotal > 4) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} лимит 'SMESBasic/SMESBasicEmpty' - 4, обнаружено {smesBasicTotal}."); }
                            else if (smesAdvancedTotal > 0)
                            { if (smesAdvancedTotal > 4) sb.AppendLine($"[Энергия] {vessel.ID}: для {sizeRuName} лимит 'SMESAdvanced/SMESAdvancedEmpty' - 4, обнаружено {smesAdvancedTotal}."); }
                            break;
                    }
                    if (debugFound.Count > 0) sb.AppendLine($"[Дебаг] {vessel.ID}: найдены debug-прототипы: {string.Join(", ", debugFound.Distinct())}.");
                    bool hasWarp = false;
                    var warpQuery = entManager.EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
                    while (warpQuery.MoveNext(out _, out var wXform))
                    { if (wXform.GridUid == gridUid) { hasWarp = true; break; } }
                    if (!hasWarp) sb.AppendLine($"[Варп] {vessel.ID}: на сетке шаттла отсутствует WarpPoint.");
                    if (sb.Length > 0)
                    {
                        sb.AppendLine($"[Карта] {vessel.ID}: {vessel.ShuttlePath}");
                        Assert.Fail(sb.ToString());
                    }
                    try
                    { map.DeleteMap(mapId); }
                    catch (Exception ex)
                    { Assert.Fail($"Не удалось удалить карту для {vessel} ({vessel.ShuttlePath}): {ex}"); }
                }
            });
        });
        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }
}


