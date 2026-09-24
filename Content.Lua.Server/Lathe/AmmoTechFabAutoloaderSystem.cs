// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Lua.Shared.Lathe;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Lua.Server.Lathe;

public sealed class AmmoTechFabAutoloaderSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly LatheSystem _lathe = default!;
    [Dependency] private readonly MaterialStorageSystem _materials = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private readonly Dictionary<EntProtoId, List<RoundPrice>> _prices = new();
    private readonly Dictionary<EntProtoId, List<EntProtoId>> _fits = new();
    private readonly HashSet<ProtoId<LatheRecipePrototype>> _available = new();
    private readonly Dictionary<ProtoId<MaterialPrototype>, int> _spend = new();
    private readonly List<EntProtoId> _choiceScratch = new();
    private List<EntityPrototype> _cartridgePrototypes = new();

    private EntityQuery<AmmoTechFabAutoloadComponent> _orderQuery;
    private EntityQuery<GunComponent> _gunQuery;
    private EntityQuery<ItemComponent> _itemQuery;
    private EntityQuery<LatheComponent> _latheQuery;
    private EntityQuery<MaterialStorageComponent> _storageQuery;

    public override void Initialize()
    {
        base.Initialize();

        _orderQuery = GetEntityQuery<AmmoTechFabAutoloadComponent>();
        _gunQuery = GetEntityQuery<GunComponent>();
        _itemQuery = GetEntityQuery<ItemComponent>();
        _latheQuery = GetEntityQuery<LatheComponent>();
        _storageQuery = GetEntityQuery<MaterialStorageComponent>();

        SubscribeLocalEvent<AmmoTechFabAutoloaderComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AmmoTechFabAutoloaderComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AmmoTechFabAutoloaderComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<AmmoTechFabAutoloaderComponent, GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<AmmoTechFabAutoloaderComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<AmmoTechFabAutoloaderComponent, TechnologyDatabaseModifiedEvent>(OnTechModified);
        SubscribeLocalEvent<AmmoTechFabAutoloaderComponent, AmmoTechFabEjectMessage>(OnEjectMessage);
        SubscribeLocalEvent<AmmoTechFabAutoloaderComponent, AmmoTechFabEjectAllMessage>(OnEjectAllMessage);
        SubscribeLocalEvent<AmmoTechFabAutoloaderComponent, AmmoTechFabSetCartridgeMessage>(OnSetCartridgeMessage);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildCaches();
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<AmmoTechFabAutoloaderComponent>();
        while (query.MoveNext(out var uid, out var loader))
        {
            if (!_latheQuery.TryComp(uid, out var lathe) || !_storageQuery.TryComp(uid, out var storage))
                continue;

            loader.ChoiceRefresh += frameTime;
            var refreshOrders = loader.ChoiceRefresh >= 1f;
            if (refreshOrders)
                loader.ChoiceRefresh = 0f;

            if (!_container.TryGetContainer(uid, loader.Container, out var container) || container.Count == 0)
            {
                SetStarved(uid, loader, false);
                continue;
            }

            if (!this.IsPowered(uid, EntityManager))
                continue;

            RefreshAvailable(uid, lathe);
            if (refreshOrders)
                RefreshAllOrders(uid, lathe, container);

            loader.Budget = Math.Min(loader.Budget + frameTime * loader.MaxRoundsPerSecond, loader.MaxRoundsPerSecond);

            var count = container.ContainedEntities.Count;
            var start = loader.Cursor % count;
            var missingMaterials = false;
            var loaded = false;

            for (var n = 0; n < count; n++)
            {
                var mag = container.ContainedEntities[(start + n) % count];
                if (!TickMagazine(uid, loader, lathe, storage, mag, frameTime, ref missingMaterials, ref loaded))
                    break;
            }

            loader.Cursor = (start + 1) % count;
            SetStarved(uid, loader, missingMaterials && !loaded);

            if (loaded && loader.SoundLoad != null && _timing.CurTime >= loader.NextSound)
            {
                loader.NextSound = _timing.CurTime + loader.SoundInterval;
                _audio.PlayPvs(loader.SoundLoad, uid);
            }
        }
    }

    private bool TickMagazine(
        EntityUid uid,
        AmmoTechFabAutoloaderComponent loader,
        LatheComponent lathe,
        MaterialStorageComponent storage,
        EntityUid mag,
        float frameTime,
        ref bool missingMaterials,
        ref bool loaded)
    {
        if (!_guns.TryGetBallisticInfo(mag, out var info) || info.Infinite || info.Capacity <= 0)
            return true;

        if (!_orderQuery.TryComp(mag, out var order))
            order = EnsureOrder(uid, lathe, mag);

        if (info.Count >= info.Capacity)
            SetWaiting(mag, order, false);

        if (!order.Selected || order.Cartridge is not { } cartridge || info.Count >= info.Capacity)
            return true;

        var sameType = info.Count == 0 || info.Proto == cartridge;
        var can = sameType && FindPrice(cartridge) != null && MagazineAccepts(mag, cartridge);
        if (order.CanLoad != can)
        {
            order.CanLoad = can;
            DirtyField(mag, order, nameof(AmmoTechFabAutoloadComponent.CanLoad));
        }

        if (!can)
            return true;

        if (info.Count == 0)
            _guns.TryAssignBallisticProto(mag, cartridge);

        order.Accumulator = Math.Min(order.Accumulator + frameTime * loader.RoundsPerSecond, 3f);

        while (order.Accumulator >= 1f && loader.Budget >= 1f)
        {
            if (!_guns.TryGetBallisticInfo(mag, out info) || info.Count >= info.Capacity)
                return true;

            if (!TrySpendRound(uid, storage, lathe, cartridge, info.Count))
            {
                missingMaterials = true;
                order.Accumulator = Math.Min(order.Accumulator, 1f);
                SetWaiting(mag, order, true);
                return true;
            }

            if (!_guns.TryAddUnspawnedRound(mag, cartridge))
            {
                RefundLastSpend(uid, storage);
                order.Accumulator = Math.Min(order.Accumulator, 1f);
                return true;
            }

            SetWaiting(mag, order, false);

            order.Accumulator -= 1f;
            loader.Budget -= 1f;
            loaded = true;
        }

        return true;
    }

    private void OnInit(EntityUid uid, AmmoTechFabAutoloaderComponent component, ComponentInit args)
    {
        _container.EnsureContainer<Container>(uid, component.Container);
    }

    private void OnInteractUsing(EntityUid uid, AmmoTechFabAutoloaderComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !_guns.TryGetBallisticInfo(args.Used, out _))
            return;

        if (!IsLoadable(args.Used))
        {
            if (!_gunQuery.HasComponent(args.Used))
            {
                _popup.PopupEntity(Loc.GetString("ammo-techfab-autoloader-reject"), uid, args.User);
                args.Handled = true;
            }

            return;
        }

        var container = _container.EnsureContainer<Container>(uid, component.Container);
        if (!_container.Insert(args.Used, container))
        {
            _popup.PopupEntity(Loc.GetString("ammo-techfab-autoloader-insert-fail"), uid, args.User);
            args.Handled = true;
            return;
        }

        if (_latheQuery.TryComp(uid, out var lathe))
        {
            RefreshAvailable(uid, lathe);
            var order = EnsureOrder(uid, lathe, args.Used);
            TryInheritGroupSelection(uid, args.Used, order, container);
        }

        _popup.PopupEntity(
            Loc.GetString("ammo-techfab-autoloader-inserted", ("magazine", Name(args.Used))),
            uid,
            args.User);
        args.Handled = true;
    }

    private void OnExamine(EntityUid uid, AmmoTechFabAutoloaderComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var count = 0;
        if (_container.TryGetContainer(uid, component.Container, out var container))
            count = container.ContainedEntities.Count;

        args.PushMarkup(Loc.GetString("ammo-techfab-autoloader-examine",
            ("count", count),
            ("rate", (int) component.RoundsPerSecond)));
    }

    private void OnVerbs(EntityUid uid, AmmoTechFabAutoloaderComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_container.TryGetContainer(uid, component.Container, out var container) || container.Count == 0)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("ammo-techfab-autoloader-eject-all"),
            Act = () => EjectAll(uid, component, args.User),
        });
    }

    private void OnDestruction(EntityUid uid, AmmoTechFabAutoloaderComponent component, DestructionEventArgs args)
    {
        if (_container.TryGetContainer(uid, component.Container, out var container))
            _container.EmptyContainer(container, true, Transform(uid).Coordinates);
    }

    private void OnTechModified(EntityUid uid, AmmoTechFabAutoloaderComponent component, ref TechnologyDatabaseModifiedEvent args)
    {
        if (!_latheQuery.TryComp(uid, out var lathe))
            return;

        RefreshAvailable(uid, lathe);
        if (_container.TryGetContainer(uid, component.Container, out var container))
            RefreshAllOrders(uid, lathe, container);
    }

    private void OnEjectMessage(EntityUid uid, AmmoTechFabAutoloaderComponent component, AmmoTechFabEjectMessage args)
    {
        if (!TryGetEntity(args.Magazine, out var magazine) || magazine is not { } mag)
            return;

        Eject(uid, component, mag, args.Actor);
    }

    private void OnEjectAllMessage(EntityUid uid, AmmoTechFabAutoloaderComponent component, AmmoTechFabEjectAllMessage args)
    {
        EjectAll(uid, component, args.Actor);
    }

    private void OnSetCartridgeMessage(EntityUid uid, AmmoTechFabAutoloaderComponent component, AmmoTechFabSetCartridgeMessage args)
    {
        if (!TryGetEntity(args.Magazine, out var magazine) || magazine is not { } sample)
            return;

        if (!_container.TryGetContainer(uid, component.Container, out var container) || !container.Contains(sample))
            return;

        if (!_latheQuery.TryComp(uid, out var lathe))
            return;

        RefreshAvailable(uid, lathe);
        var sampleOrder = EnsureOrder(uid, lathe, sample);
        var cartridge = new EntProtoId(args.Cartridge);
        if (!sampleOrder.Choices.Contains(cartridge))
            return;

        var group = sampleOrder.Group;
        if (string.IsNullOrEmpty(group))
            group = GetGroupKey(sample);

        foreach (var magazineUid in container.ContainedEntities)
        {
            var order = EnsureOrder(uid, lathe, magazineUid);
            if (order.Group != group && GetGroupKey(magazineUid) != group)
                continue;

            if (!_guns.TryGetBallisticInfo(magazineUid, out var info) || info.Count > 0)
                continue;

            if (!order.Choices.Contains(cartridge) && !MagazineAccepts(magazineUid, cartridge))
                continue;

            order.Cartridge = cartridge;
            order.Selected = true;
            order.CanLoad = FindPrice(cartridge) != null;
            DirtyField(magazineUid, order, nameof(AmmoTechFabAutoloadComponent.Cartridge));
            DirtyField(magazineUid, order, nameof(AmmoTechFabAutoloadComponent.Selected));
            DirtyField(magazineUid, order, nameof(AmmoTechFabAutoloadComponent.CanLoad));
            _guns.TryAssignBallisticProto(magazineUid, cartridge);
        }
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>() && !args.WasModified<LatheRecipePrototype>())
            return;

        BuildCaches();
    }

    private void Eject(EntityUid uid, AmmoTechFabAutoloaderComponent component, EntityUid magazine, EntityUid user)
    {
        if (!_container.TryGetContainer(uid, component.Container, out var container) || !container.Contains(magazine))
            return;

        if (!_container.Remove(magazine, container, destination: Transform(uid).Coordinates))
            return;

        _hands.TryPickupAnyHand(user, magazine);
    }

    private void EjectAll(EntityUid uid, AmmoTechFabAutoloaderComponent component, EntityUid user)
    {
        if (!_container.TryGetContainer(uid, component.Container, out var container) || container.Count == 0)
            return;

        var removed = _container.EmptyContainer(container, true, Transform(uid).Coordinates);
        foreach (var magazine in removed)
            _hands.TryPickupAnyHand(user, magazine);
    }

    private AmmoTechFabAutoloadComponent EnsureOrder(EntityUid lathe, LatheComponent latheComponent, EntityUid magazine)
    {
        var order = EnsureComp<AmmoTechFabAutoloadComponent>(magazine);
        RefreshOrder(lathe, latheComponent, magazine, order);
        return order;
    }

    private void RefreshAllOrders(EntityUid lathe, LatheComponent latheComponent, BaseContainer container)
    {
        foreach (var magazine in container.ContainedEntities)
        {
            if (_orderQuery.TryComp(magazine, out var order))
                RefreshOrder(lathe, latheComponent, magazine, order);
            else
                EnsureOrder(lathe, latheComponent, magazine);
        }
    }

    private void RefreshOrder(EntityUid latheUid, LatheComponent lathe, EntityUid magazine, AmmoTechFabAutoloadComponent order)
    {
        _choiceScratch.Clear();

        var hasRounds = _guns.TryGetBallisticInfo(magazine, out var info) && info.Count > 0;
        if (hasRounds && info.Proto is { } locked)
            _choiceScratch.Add(locked);
        else
            CollectChoices(magazine);

        var choicesChanged = !SameChoices(order.Choices, _choiceScratch);
        if (choicesChanged)
            order.Choices = new List<EntProtoId>(_choiceScratch);

        var group = GetGroupKey(magazine);
        var groupChanged = order.Group != group;
        order.Group = group;

        var previous = order.Cartridge;
        var selected = order.Selected;
        if (hasRounds)
        {
            if (order.Choices.Count > 0)
                order.Cartridge = order.Choices[0];
            else
                order.Cartridge = null;
            selected = order.Cartridge != null;
        }
        else if (previous == null || !order.Choices.Contains(previous.Value))
        {
            order.Cartridge = null;
            selected = false;
        }

        var can = selected && order.Cartridge is { } cartridge && FindPrice(cartridge) != null;
        var changed = choicesChanged || groupChanged || order.CanLoad != can || order.Selected != selected || previous != order.Cartridge;
        order.Selected = selected;
        order.CanLoad = can;
        if (!changed)
            return;

        if (choicesChanged)
            DirtyField(magazine, order, nameof(AmmoTechFabAutoloadComponent.Choices));

        if (groupChanged)
            DirtyField(magazine, order, nameof(AmmoTechFabAutoloadComponent.Group));

        DirtyField(magazine, order, nameof(AmmoTechFabAutoloadComponent.Cartridge));
        DirtyField(magazine, order, nameof(AmmoTechFabAutoloadComponent.Selected));
        DirtyField(magazine, order, nameof(AmmoTechFabAutoloadComponent.CanLoad));
    }

    private void CollectChoices(EntityUid magazine)
    {
        if (MetaData(magazine).EntityPrototype is { } magazineProto &&
            _fits.TryGetValue(magazineProto.ID, out var fits))
        {
            foreach (var cartridgeId in fits)
            {
                if (FindPrice(cartridgeId) != null)
                    _choiceScratch.Add(cartridgeId);
            }

            return;
        }

        if (!_guns.TryGetBallisticInfo(magazine, out var info) || info.Whitelist == null)
            return;

        foreach (var cartridge in _cartridgePrototypes)
        {
            if (!Fits(info.Whitelist, cartridge) || FindPrice(cartridge.ID) == null)
                continue;

            _choiceScratch.Add(cartridge.ID);
        }

        _choiceScratch.Sort(CompareCartridges);
    }

    private void TryInheritGroupSelection(
        EntityUid lathe,
        EntityUid magazine,
        AmmoTechFabAutoloadComponent order,
        BaseContainer container)
    {
        if (order.Selected || !_guns.TryGetBallisticInfo(magazine, out var info) || info.Count > 0)
            return;

        var group = order.Group;
        if (string.IsNullOrEmpty(group))
            group = GetGroupKey(magazine);

        foreach (var other in container.ContainedEntities)
        {
            if (other == magazine)
                continue;

            if (!_orderQuery.TryComp(other, out var sibling) || !sibling.Selected || sibling.Cartridge is not { } cartridge)
                continue;

            if (sibling.Group != group && GetGroupKey(other) != group)
                continue;

            if (!order.Choices.Contains(cartridge))
                continue;

            order.Cartridge = cartridge;
            order.Selected = true;
            order.CanLoad = FindPrice(cartridge) != null;
            DirtyField(magazine, order, nameof(AmmoTechFabAutoloadComponent.Cartridge));
            DirtyField(magazine, order, nameof(AmmoTechFabAutoloadComponent.Selected));
            DirtyField(magazine, order, nameof(AmmoTechFabAutoloadComponent.CanLoad));
            _guns.TryAssignBallisticProto(magazine, cartridge);
            return;
        }
    }

    private string GetGroupKey(EntityUid magazine)
    {
        _choiceScratch.Clear();

        if (MetaData(magazine).EntityPrototype is { } magazineProto &&
            _fits.TryGetValue(magazineProto.ID, out var fits))
        {
            foreach (var cartridge in fits)
                _choiceScratch.Add(cartridge);
        }
        else if (_guns.TryGetBallisticInfo(magazine, out var info) && info.Whitelist != null)
        {
            foreach (var cartridge in _cartridgePrototypes)
            {
                if (!Fits(info.Whitelist, cartridge) || !_prices.ContainsKey(cartridge.ID))
                    continue;

                _choiceScratch.Add(cartridge.ID);
            }

            _choiceScratch.Sort(CompareCartridges);
        }

        if (_choiceScratch.Count == 0)
            return MetaData(magazine).EntityPrototype?.ID ?? magazine.ToString();

        return string.Join('\0', _choiceScratch);
    }

    private bool MagazineAccepts(EntityUid magazine, EntProtoId cartridge)
    {
        if (MetaData(magazine).EntityPrototype is { } magazineProto &&
            _fits.TryGetValue(magazineProto.ID, out var fits))
            return fits.Contains(cartridge);

        if (!_guns.TryGetBallisticInfo(magazine, out var info) || info.Whitelist == null)
            return false;

        foreach (var candidate in _cartridgePrototypes)
        {
            if (candidate.ID != cartridge.Id)
                continue;

            return Fits(info.Whitelist, candidate);
        }

        return false;
    }

    private bool TrySpendRound(
        EntityUid uid,
        MaterialStorageComponent storage,
        LatheComponent lathe,
        EntProtoId cartridge,
        int loadedCount)
    {
        var price = FindPrice(cartridge);
        if (price == null)
            return false;

        _spend.Clear();
        foreach (var (material, amount) in price.Materials)
        {
            var adjusted = SharedLatheSystem.AdjustMaterial(amount, price.DiscountScale, lathe.FinalMaterialUseMultiplier);
            var marginal = Marginal(adjusted, price.BoxCapacity, loadedCount);
            if (marginal > 0)
                _spend[material] = -marginal;
        }

        if (_spend.Count == 0)
            return true;

        return _materials.TryChangeMaterialAmount((uid, storage), _spend);
    }

    private void RefundLastSpend(EntityUid uid, MaterialStorageComponent storage)
    {
        if (_spend.Count == 0)
            return;

        var refund = new Dictionary<ProtoId<MaterialPrototype>, int>(_spend.Count);
        foreach (var (material, amount) in _spend)
            refund[material] = -amount;

        _materials.TryChangeMaterialAmount((uid, storage), refund);
    }

    private RoundPrice? FindPrice(EntProtoId cartridge)
    {
        if (!_prices.TryGetValue(cartridge, out var prices))
            return null;

        foreach (var price in prices)
        {
            if (_available.Contains(price.Recipe))
                return price;
        }

        return null;
    }

    private void RefreshAvailable(EntityUid uid, LatheComponent lathe)
    {
        _available.Clear();
        foreach (var recipe in _lathe.GetAvailableRecipes(uid, lathe))
            _available.Add(recipe);
    }

    private bool IsLoadable(EntityUid uid)
    {
        if (!_itemQuery.HasComponent(uid) || _gunQuery.HasComponent(uid))
            return false;

        if (!_guns.TryGetBallisticInfo(uid, out var info) || info.Infinite || info.Capacity <= 0)
            return false;

        return MetaData(uid).EntityPrototype is { } proto && !IsAmmoBox(proto);
    }

    private void BuildCaches()
    {
        _prices.Clear();
        _fits.Clear();
        _cartridgePrototypes = CollectCartridges();

        foreach (var recipe in _prototypes.EnumeratePrototypes<LatheRecipePrototype>())
        {
            if (recipe.Abstract || recipe.Result is not { } result)
                continue;

            if (!_prototypes.TryIndex(result, out var resultProto))
                continue;

            if (!_guns.TryGetPrototypeBallisticInfo(resultProto, out var info) ||
                info.Proto is not { } cartridge ||
                info.Capacity <= 0 ||
                !IsAmmoBox(resultProto))
                continue;

            var capacity = info.Capacity * Math.Max(1, recipe.ResultCount);
            var price = new RoundPrice(recipe.ID, capacity, recipe.MaterialDiscountScale, new Dictionary<ProtoId<MaterialPrototype>, int>(recipe.Materials));

            if (!_prices.TryGetValue(cartridge, out var list))
            {
                list = new List<RoundPrice>();
                _prices.Add(cartridge, list);
            }

            var replaced = false;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].BoxCapacity != capacity)
                    continue;

                if (MaterialSum(price.Materials) > MaterialSum(list[i].Materials))
                    list[i] = price;

                replaced = true;
                break;
            }

            if (!replaced)
                list.Add(price);
        }

        foreach (var list in _prices.Values)
            list.Sort((a, b) => a.BoxCapacity.CompareTo(b.BoxCapacity));

        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!_guns.TryGetPrototypeBallisticInfo(prototype, out var info) || info.Infinite || info.Capacity <= 0)
                continue;

            if (IsAmmoBox(prototype) || info.Whitelist == null)
                continue;

            List<EntProtoId>? fits = null;
            foreach (var cartridge in _cartridgePrototypes)
            {
                if (!Fits(info.Whitelist, cartridge))
                    continue;

                if (!_prices.ContainsKey(cartridge.ID))
                    continue;

                fits ??= new List<EntProtoId>();
                fits.Add(cartridge.ID);
            }

            if (fits == null || fits.Count == 0)
                continue;

            fits.Sort(CompareCartridges);
            _fits.Add(prototype.ID, fits);
        }
    }

    private List<EntityPrototype> CollectCartridges()
    {
        var cartridges = new List<EntityPrototype>();
        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.TryGetComponent(out CartridgeAmmoComponent? _, _factory))
                cartridges.Add(prototype);
        }

        return cartridges;
    }

    private bool Fits(EntityWhitelist whitelist, EntityPrototype cartridge)
    {
        if (whitelist.Tags is not { Count: > 0 })
            return false;

        return whitelist.RequireAll
            ? _tags.PrototypeHasAllTags(cartridge, whitelist.Tags)
            : _tags.PrototypeHasAnyTag(cartridge, whitelist.Tags);
    }

    private bool IsAmmoBox(EntityPrototype prototype)
    {
        var seen = new HashSet<string>();
        var pending = new Stack<EntityPrototype>();
        pending.Push(prototype);

        while (pending.TryPop(out var current))
        {
            if (!seen.Add(current.ID))
                continue;

            if (current.ID.Contains("AmmoBox", StringComparison.OrdinalIgnoreCase) ||
                current.ID.Contains("MagazineBox", StringComparison.OrdinalIgnoreCase) ||
                current.ID == "BaseAmmoProvider")
                return true;

            if (current.TryGetComponent(out PhysicalCompositionComponent? composition, _factory) &&
                composition.MaterialComposition.ContainsKey("Cardboard"))
                return true;

            if (current.Parents == null)
                continue;

            foreach (var parentId in current.Parents)
            {
                if (_prototypes.TryIndex<EntityPrototype>(parentId, out var parent))
                    pending.Push(parent);
            }
        }

        return false;
    }

    private void SetStarved(EntityUid uid, AmmoTechFabAutoloaderComponent loader, bool starved)
    {
        if (loader.Starved == starved)
            return;

        loader.Starved = starved;
        DirtyField(uid, loader, nameof(AmmoTechFabAutoloaderComponent.Starved));
    }

    private void SetWaiting(EntityUid magazine, AmmoTechFabAutoloadComponent order, bool waiting)
    {
        if (order.WaitingMaterials == waiting)
            return;

        order.WaitingMaterials = waiting;
        DirtyField(magazine, order, nameof(AmmoTechFabAutoloadComponent.WaitingMaterials));
    }

    private static int Marginal(int total, int capacity, int index)
    {
        if (total <= 0 || capacity <= 0)
            return 0;

        return total * (index + 1) / capacity - total * index / capacity;
    }

    private static int MaterialSum(Dictionary<ProtoId<MaterialPrototype>, int> materials)
    {
        var sum = 0;
        foreach (var amount in materials.Values)
            sum += amount;

        return sum;
    }

    private static int CompareCartridges(EntProtoId a, EntProtoId b)
    {
        var rank = FmjRank(a.Id).CompareTo(FmjRank(b.Id));
        return rank != 0 ? rank : string.CompareOrdinal(a.Id, b.Id);
    }

    private static int FmjRank(string id)
    {
        if (id.EndsWith("FMJ", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (id.Contains("FMJ", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (id.Contains("Buckshot", StringComparison.OrdinalIgnoreCase))
            return 2;
        return 3;
    }

    private static bool SameChoices(List<EntProtoId> current, List<EntProtoId> next)
    {
        if (current.Count != next.Count)
            return false;

        for (var i = 0; i < current.Count; i++)
        {
            if (current[i] != next[i])
                return false;
        }

        return true;
    }

    private sealed class RoundPrice
    {
        public ProtoId<LatheRecipePrototype> Recipe;
        public int BoxCapacity;
        public float DiscountScale;
        public Dictionary<ProtoId<MaterialPrototype>, int> Materials;

        public RoundPrice(
            ProtoId<LatheRecipePrototype> recipe,
            int boxCapacity,
            float discountScale,
            Dictionary<ProtoId<MaterialPrototype>, int> materials)
        {
            Recipe = recipe;
            BoxCapacity = boxCapacity;
            DiscountScale = discountScale;
            Materials = materials;
        }
    }
}
