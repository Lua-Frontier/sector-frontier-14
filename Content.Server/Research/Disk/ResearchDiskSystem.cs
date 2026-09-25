using System.Linq;
using Content.Shared.Interaction;
using Content.Server.Popups;
using Content.Shared.Research.Prototypes;
using Content.Server.Research.Systems;
using Content.Shared.Research.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.Disk
{
    public sealed class ResearchDiskSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly ResearchSystem _research = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ResearchDiskComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<ResearchDiskComponent, MapInitEvent>(OnMapInit);
        }

        private void OnAfterInteract(EntityUid uid, ResearchDiskComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach)
                return;

            if (!TryComp<ResearchServerComponent>(args.Target, out var server))
                return;

            var target = args.Target.Value;
            if (component.UnlockFactionTechnologies || component.UnlockAllTechnologies)
            {
                if (!TryComp<TechnologyDatabaseComponent>(target, out var database)) return;
                var unlocked = 0;
                foreach (var tech in _prototype.EnumeratePrototypes<TechnologyPrototype>())
                {
                    if (_research.IsTechnologyUnlocked(target, tech, database)) continue;
                    if (!component.UnlockAllTechnologies)
                    {
                        if (tech.Hidden) continue;
                        if (!_research.IsTechnologyFactionAllowed(target, tech)) continue;
                    }
                    _research.AddTechnology(target, tech, database);
                    unlocked++;
                }
                _research.UpdateTechnologyCards(target, database);
                foreach (var client in server.Clients.ToList())
                { _research.SyncClientWithServer(client); }
                _popupSystem.PopupEntity(Loc.GetString("research-disk-unlocked-technologies", ("count", unlocked)), target, args.User);
                QueueDel(uid);
                args.Handled = true;
                return;
            }
            _research.ModifyServerPoints(target, component.Points, server);
            _popupSystem.PopupEntity(Loc.GetString("research-disk-inserted", ("points", component.Points)), target, args.User);
            QueueDel(uid);
            args.Handled = true;
        }

        private void OnMapInit(EntityUid uid, ResearchDiskComponent component, MapInitEvent args)
        {
            if (!component.UnlockAllTech)
                return;

            component.Points = _prototype.EnumeratePrototypes<TechnologyPrototype>()
                .Sum(tech => tech.Cost);
        }
    }
}
