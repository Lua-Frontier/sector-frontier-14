// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Net;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Lua.Info;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._Lua.Info;

public sealed class PublicOfferManager : IPublicOfferGate
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static DateTime LastValidReadTime => DateTime.UtcNow - TimeSpan.FromDays(60);

    public void Initialize()
    {
        _netManager.Connected += OnConnected;
        _netManager.RegisterNetMessage<SendPublicOfferInformationMessage>();
        _netManager.RegisterNetMessage<PublicOfferAcceptedMessage>(OnPublicOfferAccepted);
    }

    public async Task<bool> HasAcceptedOfferAsync(NetUserId userId)
    {
        return await _dbManager.GetAcceptedPublicOffer(userId) != null;
    }

    private async void OnConnected(object? sender, NetChannelArgs e)
    {
        var isLocalhost = IPAddress.IsLoopback(e.Channel.RemoteEndPoint.Address) &&
                          _cfg.GetCVar(CCVars.RulesExemptLocal);

        if (isLocalhost)
            return;

        var acceptedOffer = await _dbManager.GetAcceptedPublicOffer(e.Channel.UserId);
        if (acceptedOffer != null)
            return;

        var lastRead = await _dbManager.GetLastReadRules(e.Channel.UserId);
        var hasCooldown = lastRead > LastValidReadTime;

        var message = new SendPublicOfferInformationMessage
        {
            PendingRulesPopupTime = _cfg.GetCVar(CCVars.RulesWaitTime),
            PendingCoreRules = _cfg.GetCVar(CCVars.RulesFile),
            PendingShouldShowRules = !hasCooldown,
        };

        _netManager.ServerSendMessage(message, e.Channel);
    }

    private async void OnPublicOfferAccepted(PublicOfferAcceptedMessage message)
    {
        await _dbManager.SetAcceptedPublicOffer(message.MsgChannel.UserId, DateTime.UtcNow);
    }
}
