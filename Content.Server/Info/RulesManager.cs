using System.Net;
using Content.Server.Database;
using Content.Shared._Lua.Info;
using Content.Shared.CCVar;
using Content.Shared.Info;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server.Info;

public sealed class RulesManager
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPublicOfferGate _publicOfferGate = default!; // Lua

    private static DateTime LastValidReadTime => DateTime.UtcNow - TimeSpan.FromDays(60);

    public void Initialize()
    {
        _netManager.Connected += OnConnected;
        _netManager.RegisterNetMessage<SendRulesInformationMessage>();
        _netManager.RegisterNetMessage<RulesAcceptedMessage>(OnRulesAccepted);
    }

    private async void OnConnected(object? sender, NetChannelArgs e)
    {
         var isLocalhost = IPAddress.IsLoopback(e.Channel.RemoteEndPoint.Address) &&
                               _cfg.GetCVar(CCVars.RulesExemptLocal);

        var lastRead = await _dbManager.GetLastReadRules(e.Channel.UserId);
        var hasCooldown = lastRead > LastValidReadTime;
        var acceptedOffer = await _publicOfferGate.HasAcceptedOfferAsync(e.Channel.UserId);

        var showRulesMessage = new SendRulesInformationMessage
        {
            PopupTime = _cfg.GetCVar(CCVars.RulesWaitTime),
            CoreRules = _cfg.GetCVar(CCVars.RulesFile),
            ShouldShowRules = !isLocalhost && !hasCooldown && acceptedOffer,
        };
        _netManager.ServerSendMessage(showRulesMessage, e.Channel);
    }

    private async void OnRulesAccepted(RulesAcceptedMessage message)
    {
        var date = DateTime.UtcNow;
        await _dbManager.SetLastReadRules(message.MsgChannel.UserId, date);
    }
}
