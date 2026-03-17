// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Text;

namespace Content.IntegrationTests.Tests._Lua;

/// <summary>
/// Размеры зоны drawline
///   Micro  - квадрат 9×9   = 81 тайл
///   Small  - квадрат 21×21 = 441 тайл
///   Medium - квадрат 31×31 = 961 тайл
///   Large  - лимит         = 2304 тайл
/// </summary>
[TestFixture]
public sealed class ShipyardLuaTileSizeTests
{
    // Максимальное количество тайлов для каждой категории (из drawline)
    private const int MicroMaxTiles = 81;   // 9×9
    private const int SmallMaxTiles = 441;  // 21×21
    private const int MediumMaxTiles = 961; // 31×31
    private const int LargeMaxTiles = 2304;

    private const int MinSmallTiles = 82;
    private const int MinMediumTiles = 200;
    private const int MinLargeTiles = 950;

    [Test]
    public async Task CheckShuttleTileCountMatchesCategory()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var map = entManager.System<MapSystem>();

        await server.WaitPost(() =>
        {
            var sb = new StringBuilder();
            foreach (var vessel in protoManager.EnumeratePrototypes<VesselPrototype>())
            {
                map.CreateMap(out var mapId);
                bool mapLoaded = false;
                Entity<MapGridComponent>? shuttle = null;

                try
                {
                    mapLoaded = mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out shuttle);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[Размер] {vessel.ID}: не удалось загрузить шаттл ({vessel.ShuttlePath}): {ex.Message}");
                    map.DeleteMap(mapId);
                    continue;
                }

                if (!mapLoaded || shuttle == null)
                {
                    map.DeleteMap(mapId);
                    continue;
                }

                var tileCount = map.GetAllTiles(shuttle.Value.Owner, shuttle.Value.Comp).Count();
                var category = vessel.Category;

                switch (category)
                {
                    case VesselSize.Micro:
                        if (tileCount > MicroMaxTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Micro, но тайлов {tileCount} > макс {MicroMaxTiles}. Увеличьте категорию или уменьшите шаттл. ({vessel.ShuttlePath})");
                        break;

                    case VesselSize.Small:
                        if (tileCount < MinSmallTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Small, но тайлов слишком мало ({tileCount} < мин {MinSmallTiles}). Понизьте категорию до Micro или увеличьте шаттл. ({vessel.ShuttlePath})");
                        if (tileCount > SmallMaxTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Small, но тайлов {tileCount} > макс {SmallMaxTiles}. Увеличьте категорию до Medium или уменьшите шаттл. ({vessel.ShuttlePath})");
                        break;

                    case VesselSize.Medium:
                        if (tileCount < MinMediumTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Medium, но тайлов слишком мало ({tileCount} < мин {MinMediumTiles}). Понизьте категорию до Small или увеличьте шаттл. ({vessel.ShuttlePath})");
                        if (tileCount > MediumMaxTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Medium, но тайлов {tileCount} > макс {MediumMaxTiles}. Увеличьте категорию до Large или уменьшите шаттл. ({vessel.ShuttlePath})");
                        break;

                    case VesselSize.Large:
                        if (tileCount < MinLargeTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Large, но тайлов слишком мало ({tileCount} < мин {MinLargeTiles}). Понизьте категорию до Medium или увеличьте шаттл. ({vessel.ShuttlePath})");
                        if (tileCount > LargeMaxTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Large, но тайлов {tileCount} > макс {LargeMaxTiles}. Шаттл слишком большой. ({vessel.ShuttlePath})");
                        break;
                }

                try
                {
                    map.DeleteMap(mapId);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[Размер] {vessel.ID}: не удалось удалить карту ({vessel.ShuttlePath}): {ex.Message}");
                }
            }

            if (sb.Length > 0)
                Assert.Fail(sb.ToString());
        });

        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }
}
