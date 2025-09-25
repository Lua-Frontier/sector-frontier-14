using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lua;

[TestFixture]
public sealed class ShipyardLuaPriceTests
{
    private static readonly string[] PriceWhitelist =
    {
        "CourierRed",
        "CourierBlue",
    };

    [Test]
    public async Task CheckPriceNotExceedAppraiseBy30Percent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var map = entManager.System<MapSystem>();
        var pricing = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<PricingSystem>();
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var vessel in protoManager.EnumeratePrototypes<VesselPrototype>())
                {
                    if (PriceWhitelist.Contains(vessel.ID)) continue;
                    map.CreateMap(out var mapId);
                    double appraisePrice = 0;
                    bool mapLoaded = false;
                    Entity<MapGridComponent>? shuttle = null;
                    try { mapLoaded = mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out shuttle); }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Не удалось загрузить шаттл {vessel} ({vessel.ShuttlePath}): TryLoadGrid выбросил исключение {ex}");
                        map.DeleteMap(mapId); continue;
                    }
                    Assert.That(mapLoaded, Is.True, $"Не удалось загрузить шаттл {vessel} ({vessel.ShuttlePath}): TryLoadGrid вернул false.");
                    Assert.That(entManager.HasComponent<MapGridComponent>(shuttle.Value), Is.True);
                    if (!mapLoaded) continue;
                    pricing.AppraiseGrid(shuttle.Value, null, (uid, price) =>
                    { appraisePrice += price; });
                    var allowedMinPrice = appraisePrice * vessel.MinPriceMarkup;
                    var allowedMaxPrice = appraisePrice * 1.3f;
                    Assert.That(vessel.Price, Is.InRange(allowedMinPrice, allowedMaxPrice), $"Цена {vessel.ID} вне допустимого диапазона. Минимальная цена: {allowedMinPrice}. Максимальная цена: {allowedMaxPrice}. Оценка: {appraisePrice}. Минимальная наценка: {(vessel.MinPriceMarkup - 1.0f) * 100}%. Текущая цена: {vessel.Price}.");
                    try { map.DeleteMap(mapId); }
                    catch (Exception ex)
                    { Assert.Fail($"Не удалось удалить карту для {vessel} ({vessel.ShuttlePath}): {ex}"); }
                }
            });
        });
        await pair.CleanReturnAsync();
    }
}


