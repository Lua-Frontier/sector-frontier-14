// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._NF.Bank;
using Content.Server.Actions;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Sponsors;
using Content.Server.Store.Conditions;
using Content.Server.Store.Systems;
using Content.Shared._Lua.DonateShop;
using Content.Shared._Lua.SponsorLoadout;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Lua.CLVar;
using Content.Shared.Mind;
using Content.Shared.Store;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Content.Server._Lua.DonateShop;

public sealed class DonateShopSystem : EntitySystem
{
    [Dependency] private readonly SponsorManager _sponsorManager = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IAdminLogManager _admin = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    private static readonly SoundSpecifier BuySound = new SoundPathSpecifier("/Audio/Items/appraiser.ogg");
    private readonly HashSet<(int RoundId, NetUserId UserId, string ListingId)> _roundPurchases = new();
    private readonly HttpClient _httpClient = new();
    private readonly ConcurrentDictionary<Guid, (long Balance, DateTime ExpiresAt)> _lunaCoinCache = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _playerPurchaseLocks = new();
    private string _lunaCoinApiUrl = string.Empty;
    private string _lunaCoinApiToken = string.Empty;
    private ISawmill _sawmill = default!;

    private static readonly HashSet<ProtoId<StoreCategoryPrototype>> DonateShopCategories =
    [
        "UplinkVipHardsuits",
        "UplinkVipClothing",
        "UplinkVipCloaks",
        "UplinkVipBedsheets",
        "UplinkVipUseful",
        "UplinkVipFuel",
        "UplinkVipBackpack",
        "UplinkVipGun",
        "UplinkVipAmmo",
        "UplinkVipNocat",
        "UplinkVipFlatpack",
        "UplinkVipCrates",
        "UplinkVipTierShareholder",
        "UplinkVipTierGod",
        "UplinkVipTierRank1",
        "UplinkVipTierRank2",
        "UplinkVipTierRank3",
    ];

    private static readonly ProtoId<CurrencyPrototype>[] CurrencyWhitelist =
    [
        "Speso",
        "LunaCoin",
    ];

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("donate_shop");
        _cfg.OnValueChanged(CLVars.LunaCoinApiUrl, v => _lunaCoinApiUrl = v, true);
        _cfg.OnValueChanged(CLVars.LunaCoinApiToken, v => _lunaCoinApiToken = v, true);
        SubscribeNetworkEvent<RequestDonateShopStateMessage>(OnRequestState);
        SubscribeNetworkEvent<RequestDonateShopOpenMessage>(OnRequestOpenDonateShop);
        SubscribeNetworkEvent<RequestDonateShopBuyMessage>(OnRequestBuy);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (ev.Player.Status != SessionStatus.Disconnected) return;
        _lunaCoinCache.TryRemove(ev.Player.UserId.UserId, out _);
        _playerPurchaseLocks.TryRemove(ev.Player.UserId.UserId, out var playerLock);
        playerLock?.Dispose();
    }

    private async void OnRequestState(RequestDonateShopStateMessage msg, EntitySessionEventArgs args)
    { await SendStateAsync(args.SenderSession); }
    private async void OnRequestOpenDonateShop(RequestDonateShopOpenMessage msg, EntitySessionEventArgs args)
    { await SendStateAsync(args.SenderSession); }

    private async void OnRequestBuy(RequestDonateShopBuyMessage msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var sponsors = await GetAllShopDonorsAsync(session);
        if (sponsors.Count == 0)
        {
            await SendStateAsync(session, "donate-shop-error-access-denied");
            return;
        }
        if (session.AttachedEntity is not { Valid: true } player || HasComp<GhostComponent>(player))
        {
            await SendStateAsync(session, "donate-shop-error-no-entity");
            return;
        }

        var userId = session.UserId.UserId;
        var playerLock = _playerPurchaseLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await playerLock.WaitAsync();
        try
        {
            var lunaCoinBalance = await GetLunaCoinBalanceAsync(userId);
            var catalog = BuildCatalog(player, session.UserId, lunaCoinBalance);
            var availableListings = GetAvailableDonateListings(player, catalog).ToDictionary(l => l.ID, StringComparer.Ordinal);
            if (!availableListings.TryGetValue(msg.ListingId, out var listing))
            {
                await SendStateAsync(session);
                return;
            }
            if (IsRoundLimitedAndAlreadyPurchased(session.UserId, listing))
            {
                await SendStateAsync(session);
                return;
            }
            var lunaCost = GetLunaCoinCost(listing);
            if (lunaCost > 0)
            {
                var spendResult = await SpendLunaCoinAsync(userId, lunaCost, $"DonateShop purchase: {msg.ListingId}");
                if (!spendResult.Success)
                {
                    var errorKey = spendResult.InsufficientFunds
                        ? "donate-shop-error-lunacoin-insufficient"
                        : "donate-shop-error-lunacoin-spend-failed";
                    await SendStateAsync(session, errorKey);
                    return;
                }
                if (_lunaCoinCache.TryGetValue(userId, out var cached))
                    _lunaCoinCache[userId] = (Math.Max(0, cached.Balance - lunaCost), DateTime.UtcNow.AddSeconds(60));
            }
            if (listing.Cost.TryGetValue("Speso", out var spesoAmount) && spesoAmount > FixedPoint2.Zero)
            {
                if (!_bank.TryBankWithdraw(player, spesoAmount.Int()))
                {
                    await SendStateAsync(session, "donate-shop-error-insufficient-funds");
                    return;
                }
            }
            ExecutePurchase(player, listing);
            if (HasLimitedStock(listing))_roundPurchases.Add((_gameTicker.RoundId, session.UserId, listing.ID));
            await SendStateAsync(session);
        }
        finally
        { playerLock.Release(); }
    }

    private void ExecutePurchase(EntityUid buyer, ListingDataWithCostModifiers listing)
    {
        if (listing.ProductEntity != null)
        {
            var product = Spawn(listing.ProductEntity, Transform(buyer).Coordinates);
            _hands.PickupOrDrop(buyer, product);
        }
        if (!string.IsNullOrWhiteSpace(listing.ProductAction))
        {
            if (!_mind.TryGetMind(buyer, out var mind, out _))_actions.AddAction(buyer, listing.ProductAction);
            else _actionContainer.AddAction(mind, listing.ProductAction);
        }
        if (listing.ProductEvent != null)
        {
            if (!listing.RaiseProductEventOnUser)RaiseLocalEvent(listing.ProductEvent);
            else RaiseLocalEvent(buyer, listing.ProductEvent);
        }
        _admin.Add(LogType.StorePurchase, LogImpact.Low, $"{ToPrettyString(buyer):player} purchased donate shop listing \"{ListingLocalisationHelpers.GetLocalisedNameOrEntityName(listing, _prototypes)}\"");
        listing.PurchaseAmount++;
        _audio.PlayEntity(BuySound, buyer, buyer);
    }

    private async Task SendStateAsync(ICommonSession session, string? errorLocKey = null)
    {
        var sponsors = await GetAllShopDonorsAsync(session);
        var hasSubscription = sponsors.Count > 0;
        var lunaCoinBalance = await GetLunaCoinBalanceAsync(session.UserId.UserId);
        if (!hasSubscription)
        {
            if (session.AttachedEntity is { Valid: true } playerNoSub && !HasComp<GhostComponent>(playerNoSub))
            {
                var catalogNoSub = BuildCatalog(playerNoSub, session.UserId, lunaCoinBalance);
                var listingsNoSub = catalogNoSub.Where(l => _store.ListingHasCategory(l, DonateShopCategories)).ToHashSet();
                var bankBalanceNoSub = TryComp<BankAccountComponent>(playerNoSub, out var bankNoSub) ? bankNoSub.Balance : 0;
                var hasBankNoSub = HasComp<BankAccountComponent>(playerNoSub);
                var balanceNoSub = BuildBalance(bankBalanceNoSub, hasBankNoSub, lunaCoinBalance);
                RaiseNetworkEvent(new DonateShopStateMessage(false, false, string.Empty, string.Empty, listingsNoSub, balanceNoSub, bankBalanceNoSub, hasBankNoSub, lunaCoinBalance: lunaCoinBalance), session);
                return;
            }
            RaiseNetworkEvent(new DonateShopStateMessage(false, false, string.Empty, string.Empty, lunaCoinBalance: lunaCoinBalance), session);
            return;
        }
        var primary = sponsors.OrderByDescending(s => s.StartDate).First();
        var activeTierNames = sponsors.Select(s => s.Role).Distinct().ToList();
        if (session.AttachedEntity is not { Valid: true } player || HasComp<GhostComponent>(player))
        {
            RaiseNetworkEvent(new DonateShopStateMessage(false, true, primary.Role, primary.PlannedEndDate.HasValue ? $"{primary.PlannedEndDate.Value:dd.MM.yyyy}" : "∞", errorLocKey: "donate-shop-error-no-entity", activeTierNames: activeTierNames, lunaCoinBalance: lunaCoinBalance), session);
            return;
        }
        var catalog = BuildCatalog(player, session.UserId, lunaCoinBalance);
        var listings = GetDisplayListings(player, catalog);
        var bankBalance = TryComp<BankAccountComponent>(player, out var bank) ? bank.Balance : 0;
        var hasBankBalance = HasComp<BankAccountComponent>(player);
        var balance = BuildBalance(bankBalance, hasBankBalance, lunaCoinBalance);
        var status = primary.PlannedEndDate.HasValue ? $"{primary.PlannedEndDate.Value:dd.MM.yyyy}" : "∞";
        RaiseNetworkEvent(new DonateShopStateMessage(true, true, primary.Role, status, listings, balance, bankBalance, hasBankBalance, errorLocKey, activeTierNames, lunaCoinBalance), session);
    }

    private HashSet<ListingDataWithCostModifiers> BuildCatalog(EntityUid player, NetUserId actorUserId, long lunaCoinBalance)
    {
        var catalog = _store.GetAllListings();
        foreach (var listing in catalog)
        { if (HasLimitedStock(listing) && _roundPurchases.Contains((_gameTicker.RoundId, actorUserId, listing.ID)))listing.PurchaseAmount = 1; }
        AppendPersonalListings(catalog, player, actorUserId);
        return catalog;
    }
    private IEnumerable<ListingDataWithCostModifiers> GetAvailableDonateListings(EntityUid player, HashSet<ListingDataWithCostModifiers> catalog)
    {
        var mind = _store.GetBuyerMind(player);
        foreach (var listing in catalog)
        {
            if (!_store.ListingHasCategory(listing, DonateShopCategories)) continue;
            if (listing.Conditions != null)
            {
                var args = new ListingConditionArgs(mind, null, listing, EntityManager);
                var ok = true;
                foreach (var condition in listing.Conditions)
                {
                    if (condition is StoreWhitelistCondition) continue;
                    if (!condition.Condition(args)) { ok = false; break; }
                }
                if (!ok) continue;
            }
            yield return listing;
        }
    }

    private HashSet<ListingDataWithCostModifiers> GetDisplayListings(EntityUid player, HashSet<ListingDataWithCostModifiers> catalog)
    {
        var mind = _store.GetBuyerMind(player);
        var result = new HashSet<ListingDataWithCostModifiers>();
        foreach (var listing in catalog)
        {
            if (!_store.ListingHasCategory(listing, DonateShopCategories)) continue;
            if (listing.Conditions != null)
            {
                var args = new ListingConditionArgs(mind, null, listing, EntityManager);
                var ok = true;
                foreach (var condition in listing.Conditions)
                {
                    if (condition is ListingLimitedStockCondition) continue;
                    if (condition is BuyerSponsorTierCondition) continue;
                    if (condition is StoreWhitelistCondition) continue;
                    if (!condition.Condition(args)) { ok = false; break; }
                }
                if (!ok) continue;
            }
            result.Add(listing);
        }
        return result;
    }

    private static Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> BuildBalance(int bankBalance, bool hasBankBalance, long lunaCoinBalance)
    {
        var result = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>();
        var maxDisplayAmount = (int)FixedPoint2.MaxValue;
        foreach (var currency in CurrencyWhitelist)
        {
            result[currency] = FixedPoint2.Zero;
            if (hasBankBalance && currency == "Speso")
            {
                result[currency] = bankBalance > maxDisplayAmount ? FixedPoint2.MaxValue : FixedPoint2.New(bankBalance);
                continue;
            }
            if (currency == "LunaCoin")
            {
                var clamped = lunaCoinBalance switch { < 0 => 0, > int.MaxValue => int.MaxValue, _ => (int)lunaCoinBalance };
                result[currency] = FixedPoint2.New(clamped);
            }
        }
        return result;
    }
    private async Task<long> GetLunaCoinBalanceAsync(Guid userId)
    {
        if (string.IsNullOrEmpty(_lunaCoinApiUrl) || string.IsNullOrEmpty(_lunaCoinApiToken)) return 0;
        var now = DateTime.UtcNow;
        if (_lunaCoinCache.TryGetValue(userId, out var cached) && cached.ExpiresAt > now)
            return cached.Balance;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_lunaCoinApiUrl}/api/lunacoin/balance/{userId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _lunaCoinApiToken);
            using var response = await _httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode) return 0;
            var json = await response.Content.ReadAsStringAsync(cts.Token);
            var doc = JsonDocument.Parse(json);
            var balance = doc.RootElement.GetProperty("balance").GetInt64();
            _lunaCoinCache[userId] = (balance, now.AddSeconds(60));
            return balance;
        }
        catch (Exception e)
        {
            _sawmill.Warning($"GetLunaCoinBalance failed for {userId}: {e.Message}");
            return 0;
        }
    }

    private static long GetLunaCoinCost(ListingDataWithCostModifiers listing)
    {
        if (!listing.Cost.TryGetValue("LunaCoin", out var amount))
            return 0;

        return Math.Max(0, (long) (int) amount);
    }

    private async Task<(bool Success, bool InsufficientFunds)> SpendLunaCoinAsync(Guid userId, long amount, string comment)
    {
        if (amount <= 0)
            return (true, false);

        if (string.IsNullOrEmpty(_lunaCoinApiUrl) || string.IsNullOrEmpty(_lunaCoinApiToken))
            return (false, false);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(7));
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_lunaCoinApiUrl}/api/lunacoin/spend");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _lunaCoinApiToken);
            request.Content = JsonContent.Create(new LunaCoinSpendRequest(userId, amount, comment));

            using var response = await _httpClient.SendAsync(request, cts.Token);
            if (response.IsSuccessStatusCode)
                return (true, false);

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            var insufficient = body.Contains("Недостаточно LunaCoin", StringComparison.OrdinalIgnoreCase)
                || body.Contains("insufficient", StringComparison.OrdinalIgnoreCase);

            _sawmill.Warning($"LunaCoin spend failed for {userId}: {response.StatusCode} - {body}");
            return (false, insufficient);
        }
        catch (Exception e)
        {
            _sawmill.Warning($"LunaCoin spend exception for {userId}: {e.Message}");
            return (false, false);
        }
    }

    private sealed record LunaCoinSpendRequest(Guid UserId, long Amount, string Comment);

    private void AppendPersonalListings(HashSet<ListingDataWithCostModifiers> catalog, EntityUid player, NetUserId actorUserId)
    {
        if (!TryComp<ActorComponent>(player, out var actor)) return;
        var playerName = actor.PlayerSession.Name;
        foreach (var loadout in _prototypes.EnumeratePrototypes<SponsorLoadoutPrototype>())
        {
            if (!string.Equals(loadout.OwnerLogin, playerName, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrWhiteSpace(loadout.Tier)) continue;
            var tierCategory = LoadoutTierToCategoryId(loadout.Tier);
            foreach (var entityId in loadout.Entities)
            {
                var listingId = $"SponsorPersonal_{loadout.ID}_{entityId}";
                if (catalog.Any(x => x.ID == listingId)) continue;
                var alreadyPurchased = _roundPurchases.Contains((_gameTicker.RoundId, actorUserId, listingId)) ? 1 : 0;
                var listingData = new ListingData(
                    name: null,
                    discountCategory: null,
                    description: null,
                    conditions: new List<ListingCondition>
                    {
                        new BuyerSponsorOwnerCondition { OwnerLogin = loadout.OwnerLogin },
                        new ListingLimitedStockCondition { Stock = 1 }
                    },
                    icon: null,
                    priority: 1000,
                    productEntity: entityId,
                    productAction: null,
                    productUpgradeId: null,
                    productActionEntity: null,
                    productEvent: null,
                    raiseProductEventOnUser: false,
                    purchaseAmount: alreadyPurchased,
                    id: listingId,
                    categories: new HashSet<ProtoId<StoreCategoryPrototype>> { tierCategory },
                    originalCost: new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>(),
                    restockTime: TimeSpan.Zero,
                    dataDiscountDownTo: new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>(),
                    disableRefund: false);
                catalog.Add(new ListingDataWithCostModifiers(listingData) { Stock = 1, PurchaseAmount = alreadyPurchased });
            }
        }
    }

    private static string LoadoutTierToCategoryId(string tier) => tier.ToLowerInvariant() switch
    {
        "god"   => "UplinkVipTierGod",
        "rank1" => "UplinkVipTierRank1",
        "rank2" => "UplinkVipTierRank2",
        "rank3" => "UplinkVipTierRank3",
        _       => "UplinkVipTierRank3",
    };
    private async Task<List<Content.Server.Database.Sponsor>> GetAllShopDonorsAsync(ICommonSession session)
    {
        if (!_playerManager.TryGetSessionById(session.UserId, out _)) return [];
        if (_sponsorManager.TryGetAllActiveSponsors(session.UserId, out var cached))
        {
            var valid = cached.Where(s => DonorGroups.IsKnownTier(s.Role)).ToList();
            if (valid.Count > 0) return valid;
        }
        var all = await _sponsorManager.GetAllActiveSponsorsAsync(session.UserId);
        var known = all.Where(s => DonorGroups.IsKnownTier(s.Role)).ToList();
        if (known.Count > 0)
        {
            _sponsorManager.CacheAllActiveSponsors(session.UserId, known);
            var primary = known.OrderByDescending(s => s.StartDate).First();
            _sponsorManager.CacheActiveSponsor(session.UserId, primary);
        }
        return known;
    }

    private bool IsRoundLimitedAndAlreadyPurchased(NetUserId userId, ListingDataWithCostModifiers listing)
    {
        if (!HasLimitedStock(listing)) return false;
        return _roundPurchases.Contains((_gameTicker.RoundId, userId, listing.ID));
    }

    private static bool HasLimitedStock(ListingDataWithCostModifiers listing)
    {
        if (listing.Conditions == null) return false;
        return listing.Conditions.Any(condition => condition is ListingLimitedStockCondition stockCondition && stockCondition.Stock <= 1);
    }
}
