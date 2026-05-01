// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Store.Systems;
using Content.Server.Sponsors;
using Content.Shared.Ghost;
using Content.Server.GameTicking;
using Content.Server.Store.Conditions;
using Content.Shared._Lua.DonateShop;
using Content.Shared._Lua.SponsorLoadout;
using Content.Shared._NF.Bank.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Lua.CLVar;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
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
    private readonly Dictionary<NetUserId, EntityUid> _donateShops = new();
    private readonly HashSet<(int RoundId, NetUserId UserId, string ListingId)> _roundPurchases = new();
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<Guid, (long Balance, DateTime ExpiresAt)> _lunaCoinCache = new();
    private string _lunaCoinApiUrl = string.Empty;
    private string _lunaCoinApiToken = string.Empty;
    private ISawmill _sawmill = default!;
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
        if (_donateShops.Remove(ev.Player.UserId, out var donateShop) && Exists(donateShop)) Del(donateShop);
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
        var donateShop = EnsureDonateShop(session.UserId, player);
        if (!TryComp<StoreComponent>(donateShop, out var store))
        {
            await SendStateAsync(session, "donate-shop-error-access-denied");
            return;
        }
        var lunaCoinBalance = await GetLunaCoinBalanceAsync(session.UserId.UserId);
        SetLunaCoinBalance(store, lunaCoinBalance);
        _store.RefreshAllListings(store);
        AppendPersonalListings(store, player, session.UserId);
        var buyerForListing = player;
        var availableListings = _store.GetAvailableListings(buyerForListing, donateShop, store).ToDictionary(listing => listing.ID, StringComparer.Ordinal);
        if (!availableListings.ContainsKey(msg.ListingId))
        {
            await SendStateAsync(session);
            return;
        }
        if (IsRoundLimitedAndAlreadyPurchased(session.UserId, availableListings[msg.ListingId]))
        {
            await SendStateAsync(session);
            return;
        }

        var listingToBuy = availableListings[msg.ListingId];
        var lunaCost = GetLunaCoinCost(listingToBuy);
        if (lunaCost > 0)
        {
            var spendResult = await SpendLunaCoinAsync(session.UserId.UserId, lunaCost, $"DonateShop purchase: {msg.ListingId}");
            if (!spendResult.Success)
            {
                var errorKey = spendResult.InsufficientFunds
                    ? "donate-shop-error-lunacoin-insufficient"
                    : "donate-shop-error-lunacoin-spend-failed";
                await SendStateAsync(session, errorKey);
                return;
            }

            if (_lunaCoinCache.TryGetValue(session.UserId.UserId, out var cached))
            {
                var newBalance = Math.Max(0, cached.Balance - lunaCost);
                _lunaCoinCache[session.UserId.UserId] = (newBalance, DateTime.UtcNow.AddSeconds(60));
            }
        }

        var purchaseBefore = availableListings[msg.ListingId].PurchaseAmount;
        var buyMessage = new StoreBuyListingMessage(msg.ListingId)
        { Actor = player, };
        RaiseLocalEvent(donateShop, buyMessage, true);
        var listingAfterBuy = store.FullListingsCatalog.FirstOrDefault(listing => listing.ID == msg.ListingId);
        if (listingAfterBuy != null && listingAfterBuy.PurchaseAmount > purchaseBefore && IsRoundLimited(listingAfterBuy)) _roundPurchases.Add((_gameTicker.RoundId, session.UserId, msg.ListingId));
        await SendStateAsync(session);
    }

    private async Task SendStateAsync(ICommonSession session, string? errorLocKey = null)
    {
        var sponsors = await GetAllShopDonorsAsync(session);
        var hasSubscription = sponsors.Count > 0;
        var lunaCoinBalance = await GetLunaCoinBalanceAsync(session.UserId.UserId);
        if (!hasSubscription)
        {
            if (session.AttachedEntity is { Valid: true } playerNoSub)
            {
                var shopNoSub = EnsureDonateShop(session.UserId, playerNoSub);
                if (TryComp<StoreComponent>(shopNoSub, out var storeNoSub))
                {
                    SetLunaCoinBalance(storeNoSub, lunaCoinBalance);
                    _store.RefreshAllListings(storeNoSub);
                    var listingsNoSub = storeNoSub.FullListingsCatalog
                        .Where(l => _store.ListingHasCategory(l, storeNoSub.Categories))
                        .ToHashSet();
                    var bankBalanceNoSub = TryComp<BankAccountComponent>(playerNoSub, out var bankNoSub) ? bankNoSub.Balance : 0;
                    var hasBankNoSub = HasComp<BankAccountComponent>(playerNoSub);
                    var balanceNoSub = BuildBalance(storeNoSub, bankBalanceNoSub, hasBankNoSub);
                    RaiseNetworkEvent(new DonateShopStateMessage(false, false, string.Empty, string.Empty, listingsNoSub, balanceNoSub, bankBalanceNoSub, hasBankNoSub, lunaCoinBalance: lunaCoinBalance), session);
                    return;
                }
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
        var donateShop = EnsureDonateShop(session.UserId, player);
        if (!TryComp<StoreComponent>(donateShop, out var store))
        {
            RaiseNetworkEvent(new DonateShopStateMessage(false, true, primary.Role, primary.PlannedEndDate.HasValue ? $"{primary.PlannedEndDate.Value:dd.MM.yyyy}" : "∞", errorLocKey: "donate-shop-error-access-denied", activeTierNames: activeTierNames, lunaCoinBalance: lunaCoinBalance), session);
            return;
        }
        SetLunaCoinBalance(store, lunaCoinBalance);
        _store.RefreshAllListings(store);
        AppendPersonalListings(store, player, session.UserId);
        var listings = GetDisplayListings(player, donateShop, store);
        var bankBalance = TryComp<BankAccountComponent>(player, out var bank) ? bank.Balance : 0;
        var hasBankBalance = HasComp<BankAccountComponent>(player);
        var balance = BuildBalance(store, bankBalance, hasBankBalance);
        var status = primary.PlannedEndDate.HasValue ? $"{primary.PlannedEndDate.Value:dd.MM.yyyy}" : "∞";
        RaiseNetworkEvent(new DonateShopStateMessage(true, true, primary.Role, status, listings, balance, bankBalance, hasBankBalance, errorLocKey, activeTierNames, lunaCoinBalance), session);
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

    private Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> BuildBalance(StoreComponent store, int bankBalance, bool hasBankBalance)
    {
        var result = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>();
        var maxDisplayAmount = (int) FixedPoint2.MaxValue;
        foreach (var currency in store.CurrencyWhitelist)
        {
            result[currency] = FixedPoint2.Zero;
            if (hasBankBalance && currency == "Speso")
            {
                result[currency] = bankBalance > maxDisplayAmount ? FixedPoint2.MaxValue : FixedPoint2.New(bankBalance);
                continue;
            }
            if (store.Balance.TryGetValue(currency, out var value)) result[currency] = value;
        }
        return result;
    }

    private static void SetLunaCoinBalance(StoreComponent store, long lunaCoinBalance)
    {
        var clamped = lunaCoinBalance switch
        {
            < 0 => 0,
            > int.MaxValue => int.MaxValue,
            _ => (int)lunaCoinBalance,
        };

        store.Balance["LunaCoin"] = FixedPoint2.New(clamped);
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

    private static readonly ProtoId<StoreCategoryPrototype>[] TierCategories =
    [
        "UplinkVipTierShareholder",
        "UplinkVipTierGod",
        "UplinkVipTierRank1",
        "UplinkVipTierRank2",
        "UplinkVipTierRank3",
    ];
    private EntityUid EnsureDonateShop(NetUserId userId, EntityUid player)
    {
        if (_donateShops.TryGetValue(userId, out var donateShop) && Exists(donateShop))
        {
            if (TryComp<StoreComponent>(donateShop, out var existingStore)) existingStore.AccountOwner = player;
            return donateShop;
        }
        donateShop = Spawn("DonateShopVirtual", Transform(player).Coordinates);
        if (TryComp<StoreComponent>(donateShop, out var store))
        {
            store.AccountOwner = player;
            foreach (var cat in TierCategories) store.Categories.Add(cat);
        }
        _donateShops[userId] = donateShop;
        return donateShop;
    }

    private void AppendPersonalListings(StoreComponent store, EntityUid player, NetUserId actorUserId)
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
                if (store.FullListingsCatalog.Any(x => x.ID == listingId)) continue;
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
                store.FullListingsCatalog.Add(new ListingDataWithCostModifiers(listingData) { Stock = 1, PurchaseAmount = alreadyPurchased });
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
    private HashSet<ListingDataWithCostModifiers> GetDisplayListings(EntityUid player, EntityUid donateShop, StoreComponent store)
    {
        var mind = _store.GetBuyerMind(player);
        var result = new HashSet<ListingDataWithCostModifiers>();
        foreach (var listing in store.FullListingsCatalog)
        {
            if (!_store.ListingHasCategory(listing, store.Categories)) continue;
            if (listing.Conditions != null)
            {
                var args = new ListingConditionArgs(mind, donateShop, listing, EntityManager);
                var ok = true;
                foreach (var condition in listing.Conditions)
                {
                    if (condition is ListingLimitedStockCondition) continue;
                    if (condition is BuyerSponsorTierCondition) continue;
                    if (!condition.Condition(args)) { ok = false; break; }
                }
                if (!ok) continue;
            }
            result.Add(listing);
        }
        return result;
    }

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
        if (!IsRoundLimited(listing)) return false;
        return _roundPurchases.Contains((_gameTicker.RoundId, userId, listing.ID));
    }

    private static bool IsRoundLimited(ListingDataWithCostModifiers listing)
    {
        if (listing.Conditions == null) return false;
        var hasOwnerCondition = listing.Conditions.Any(condition => condition is BuyerSponsorOwnerCondition);
        var hasStockOne = listing.Conditions.Any(condition => condition is ListingLimitedStockCondition stockCondition && stockCondition.Stock <= 1);
        return hasOwnerCondition && hasStockOne;
    }
}
