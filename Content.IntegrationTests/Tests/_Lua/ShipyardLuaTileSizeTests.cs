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
/// Максимумы категорий (из drawline):
///   Micro  - макс сторона 13, макс тайлов 100
///   Small  - макс сторона 21, макс тайлов 441
///   Medium - макс сторона 31, макс тайлов 961
///   Large  - макс сторона 48, макс тайлов 1412
///
/// Минимумы:
///   Small  - мин сторона 14, мин тайлов 108
///   Medium - мин сторона 22, мин тайлов 300
///   Large  - мин сторона 32, мин тайлов зависит от длины:
///   макс сторона 40 мин 400 (длинный узкий шаттл)
///   макс сторона 32 мин 600
/// </summary>
[TestFixture]
public sealed class ShipyardLuaTileSizeTests
{
    private const int MicroMaxSide = 13;
    private const int MicroMaxTiles = 100;

    private const int SmallMaxSide = 21;
    private const int SmallMaxTiles = 441;
    private const int SmallMinSide = MicroMaxSide + 1;   // 14
    private const int SmallMinTiles = 108;               // мин для Small

    private const int MediumMaxSide = 31;
    private const int MediumMaxTiles = 961;
    private const int MediumMinSide = SmallMaxSide + 1;  // 22
    private const int MediumMinTiles = 300;              // мин для Medium

    private const int LargeMaxSide = 48;
    private const int LargeMaxTiles = 1412;
    private const int LargeMinSide = MediumMaxSide + 1;  // 32
    private const int LargeMinTilesNarrow = 600;         // макс 32-39
    private const int LargeMinTilesLong = 400;           // макс 40

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

                var tiles = map.GetAllTiles(shuttle.Value.Owner, shuttle.Value.Comp).ToList();

                if (tiles.Count == 0)
                {
                    map.DeleteMap(mapId);
                    continue;
                }

                var minX = tiles.Min(t => t.X);
                var maxX = tiles.Max(t => t.X);
                var minY = tiles.Min(t => t.Y);
                var maxY = tiles.Max(t => t.Y);
                var width = maxX - minX + 1;
                var height = maxY - minY + 1;
                var maxSide = Math.Max(width, height);
                var tileCount = tiles.Count;

                var category = vessel.Category;
                var info = $"{width}×{height} ({tileCount} тайлов)";

                switch (category)
                {
                    case VesselSize.Micro:
                        if (maxSide > MicroMaxSide || tileCount > MicroMaxTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Micro, но размер {info} превышает лимит {MicroMaxSide}×{MicroMaxSide} / {MicroMaxTiles} тайлов. Увеличьте категорию или уменьшите шаттл. ({vessel.ShuttlePath})");
                        break;

                    case VesselSize.Small:
                        if (maxSide < SmallMinSide)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Small, но размер {info} — макс сторона {maxSide} < мин {SmallMinSide}. Понизьте категорию до Micro. ({vessel.ShuttlePath})");
                        if (tileCount < SmallMinTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Small, но {info} — {tileCount} тайлов < мин {SmallMinTiles}. Понизьте категорию до Micro или увеличьте шаттл. ({vessel.ShuttlePath})");
                        if (tileCount > SmallMaxTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Small, но {tileCount} тайлов > макс {SmallMaxTiles}. Увеличьте категорию до Medium. ({vessel.ShuttlePath})");
                        break;

                    case VesselSize.Medium:
                        if (maxSide < MediumMinSide)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Medium, но размер {info} — макс сторона {maxSide} < мин {MediumMinSide}. Понизьте категорию до Small. ({vessel.ShuttlePath})");
                        if (tileCount < MediumMinTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Medium, но {info} — {tileCount} тайлов < мин {MediumMinTiles}. Понизьте категорию до Small или увеличьте шаттл. ({vessel.ShuttlePath})");
                        if (tileCount > MediumMaxTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Medium, но {tileCount} тайлов > макс {MediumMaxTiles}. Увеличьте категорию до Large. ({vessel.ShuttlePath})");
                        break;

                    case VesselSize.Large:
                        var largeMinTiles = maxSide >= 40 ? LargeMinTilesLong : LargeMinTilesNarrow;
                        if (maxSide < LargeMinSide)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Large, но размер {info} — макс сторона {maxSide} < мин {LargeMinSide}. Понизьте категорию до Medium. ({vessel.ShuttlePath})");
                        if (tileCount < largeMinTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Large, но {info} — {tileCount} тайлов < мин {largeMinTiles}. Понизьте категорию до Medium или увеличьте шаттл. ({vessel.ShuttlePath})");
                        if (tileCount > LargeMaxTiles)
                            sb.AppendLine($"[Размер] {vessel.ID}: заявлен как Large, но {tileCount} тайлов > макс {LargeMaxTiles}. Шаттл слишком большой. ({vessel.ShuttlePath})");
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
