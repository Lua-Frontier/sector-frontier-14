// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.DonateShop;

namespace Content.Client._Lua.DonateShop.Systems;

public sealed class DonateShopSystem : EntitySystem
{
    public event Action<DonateShopStateMessage>? OnStateUpdated;

    public DonateShopStateMessage? LastState { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DonateShopStateMessage>(OnStateMessage);
    }

    public void RequestState()
    { RaiseNetworkEvent(new RequestDonateShopStateMessage()); }

    public void RequestOpen()
    { RaiseNetworkEvent(new RequestDonateShopOpenMessage()); }

    public void RequestBuy(string listingId)
    { RaiseNetworkEvent(new RequestDonateShopBuyMessage(listingId)); }

    private void OnStateMessage(DonateShopStateMessage msg)
    {
        LastState = msg;
        OnStateUpdated?.Invoke(msg);
    }
}
