using System.Collections.Generic;
using System.Reflection;
using Content.Client.IoC;
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
using Content.Lua.Server.SitePlayerSync;
using Content.Lua.Server.SponsorPlayer;
using Content.Server.Administration;
using Content.Server.IoC;
using Robust.Shared.Analyzers;
using Robust.Shared.IoC;
using Robust.UnitTesting;
using EntryPoint = Content.Server.Entry.EntryPoint;

namespace Content.Tests
{
    [Virtual]
    public class ContentUnitTest : RobustUnitTest
    {
        protected override void OverrideIoC()
        {
            base.OverrideIoC();
            var dependencies = IoCManager.Instance!;

            if (Project == UnitTestProject.Server)
            {
                RegisterLuaServerIoC(dependencies);
                ServerContentIoC.Register(dependencies);
            }
            else if (Project == UnitTestProject.Client)
            {
                ClientContentIoC.Register(dependencies);
            }
        }

        private static void RegisterLuaServerIoC(IDependencyCollection deps)
        {
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

        protected override Assembly[] GetContentAssemblies()
        {
            var l = new List<Assembly>
            {
                typeof(Content.Shared.Entry.EntryPoint).Assembly,
                typeof(Content.Lua.Shared.Entry.EntryPoint).Assembly,
                typeof(Content.Lua.Common.Entry.EntryPoint).Assembly,
            };

            if (Project == UnitTestProject.Server)
            {
                l.Add(typeof(EntryPoint).Assembly);
                l.Add(typeof(Content.Lua.Server.Entry.EntryPoint).Assembly);
            }
            else if (Project == UnitTestProject.Client)
            {
                l.Add(typeof(Content.Client.Entry.EntryPoint).Assembly);
                l.Add(typeof(Content.Lua.Client.Entry.EntryPoint).Assembly);
            }

            l.Add(typeof(ContentUnitTest).Assembly);

            return l.ToArray();
        }
    }
}
