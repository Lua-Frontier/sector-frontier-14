using Content.Lua.Common.ChatFilter;
using Content.Lua.Common.Info;
using Content.Lua.Common.Networking;
using Content.Lua.Common.SitePlayerSync;
using Content.Lua.Common.SponsorPlayer;
using Content.Lua.Shared.Info;
using Content.Lua.Server.Administration.UI;
using Content.Lua.Server.ChatFilter;
using Content.Lua.Server.Info;
using Content.Lua.Server.Networking;
using Content.Lua.Server.Reputation;
using Content.Lua.Server.SponsorPlayer;
using Content.Lua.Server.SitePlayerSync;
using Content.Server.Administration;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;

namespace Content.Lua.Server.Entry;

public sealed class EntryPoint : GameServer
{
    public override void PreInit()
    {
        var deps = Dependencies;
        deps.Register<ChatFilterManager>();
        deps.Register<IChatFilterManager, ChatFilterManager>();
        deps.Register<DecryptFailLogger>();
        deps.Register<IDecryptFailLogger, DecryptFailLogger>();
        deps.Register<PublicOfferManager>();
        deps.Register<IPublicOfferManager, PublicOfferManager>();
        deps.Register<IPublicOfferGate, PublicOfferManager>();
        deps.Register<SponsorMusicManager>();
        deps.Register<ISponsorMusicManager, SponsorMusicManager>();
        deps.Register<SitePlayerSyncManager>();
        deps.Register<ISitePlayerSyncManager, SitePlayerSyncManager>();
        deps.Register<IAlertLevelAdminEuiFactory, AlertLevelAdminEuiFactory>();
        deps.Register<IReputationModerationEuiFactory, ReputationModerationEuiFactory>();
    }

    public override void PostInit()
    {
        base.PostInit();
        Dependencies.Resolve<IChatFilterManager>().Initialize();
        Dependencies.Resolve<IDecryptFailLogger>().Initialize();
        Dependencies.Resolve<IPublicOfferManager>().Initialize();
        Dependencies.Resolve<ISponsorMusicManager>().Initialize();
    }
}
