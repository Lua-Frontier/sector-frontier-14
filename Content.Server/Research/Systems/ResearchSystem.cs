using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared._Mono.Company;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Research.Systems
{
    [UsedImplicitly]
    public sealed partial class ResearchSystem : SharedResearchSystem
    {
        [Dependency] private readonly IAdminLogManager _adminLog = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly AccessReaderSystem _accessReader = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        // [Dependency] private readonly RadioSystem _radio = default!; // Frontier

        private readonly HashSet<Entity<ResearchServerComponent>> ClientLookup = new(); // Frontier: not static

        public override void Initialize()
        {
            base.Initialize();
            InitializeClient();
            InitializeConsole();
            InitializeSource();
            InitializeServer();

            SubscribeLocalEvent<TechnologyDatabaseComponent, ResearchRegistrationChangedEvent>(OnDatabaseRegistrationChanged);
        }

        /// <summary>
        /// Gets a server based on its unique numeric id.
        /// </summary>
        public bool TryGetServerById(EntityUid client, int id, [NotNullWhen(true)] out EntityUid? serverUid, [NotNullWhen(true)] out ResearchServerComponent? serverComponent, EntityUid? user = null)
        {
            serverUid = null;
            serverComponent = null;

            var query = GetServers(client, user).ToList();
            foreach (var (uid, server) in query)
            {
                if (server.Id != id)
                    continue;
                serverUid = uid;
                serverComponent = server;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the names of all the servers.
        /// </summary>
        public string[] GetServerNames(EntityUid client, EntityUid? user = null)
        {
            var allServers = GetServers(client, user).ToArray();
            var list = new string[allServers.Length];

            for (var i = 0; i < allServers.Length; i++)
            {
                list[i] = allServers[i].Comp.ServerName;
            }

            return list;
        }

        /// <summary>
        /// Gets the ids of all the servers
        /// </summary>
        public int[] GetServerIds(EntityUid client, EntityUid? user = null)
        {
            var allServers = GetServers(client, user).ToArray();
            var list = new int[allServers.Length];

            for (var i = 0; i < allServers.Length; i++)
            {
                list[i] = allServers[i].Comp.Id;
            }

            return list;
        }

        public HashSet<Entity<ResearchServerComponent>> GetServers(EntityUid client, EntityUid? user = null)
        {
            ClientLookup.Clear();

            var clientXform = Transform(client);
            if (clientXform.GridUid is not { } grid)
                return ClientLookup;

            _lookup.GetGridEntities(grid, ClientLookup);

            if (!TryComp(client, out ResearchClientComponent? clientComponent))
                return ClientLookup;
            ClientLookup.RemoveWhere(server => !IsClientServerTypeCompatible(clientComponent, server.Comp, user));
            return ClientLookup;
        }

        private ProtoId<RndFactionPrototype> GetOperatorRndFaction(EntityUid? user)
        {
            if (user != null &&
                TryComp<CompanyComponent>(user, out var company) &&
                !string.IsNullOrWhiteSpace(company.CompanyName) &&
                !string.Equals(company.CompanyName, "None", StringComparison.OrdinalIgnoreCase))
            {
                return company.CompanyName;
            }

            return "Neutral";
        }

        private bool IsClientServerTypeCompatible(
            ResearchClientComponent client,
            ResearchServerComponent server,
            EntityUid? user = null)
        {
            if (user != null && HasComp<BypassInteractionChecksComponent>(user))
                return true;
            if (client.AllowedFactions.Count > 0 &&
                !client.AllowedFactions.Any(faction => faction == server.Faction))
                return false;
            if (user != null)
                return GetOperatorRndFaction(user) == server.Faction;
            return client.AllowedFactions.Count > 0;
        }
        private bool TryClearIncompatibleServerBinding(
            EntityUid client,
            ResearchClientComponent component,
            EntityUid? user)
        {
            if (component.Server is not { } serverUid)
                return false;

            if (!TryComp(serverUid, out ResearchServerComponent? server) ||
                !IsClientServerTypeCompatible(component, server, user))
            {
                UnregisterClient(client, component);
                return true;
            }

            return false;
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<ResearchServerComponent>();
            while (query.MoveNext(out var uid, out var server))
            {
                if (server.NextUpdateTime > _timing.CurTime)
                    continue;
                server.NextUpdateTime = _timing.CurTime + server.ResearchConsoleUpdateTime;

                UpdateServer(uid, (int) server.ResearchConsoleUpdateTime.TotalSeconds, server);
            }
        }
    }
}
