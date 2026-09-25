using Content.Server.Engineering.Components;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server.Engineering.EntitySystems
{
    [UsedImplicitly]
    public sealed class SpawnAfterInteractSystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly StackSystem _stackSystem = default!;
        [Dependency] private readonly TurfSystem _turfSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedMapSystem _maps = default!;
        [Dependency] private readonly PopupSystem _popup = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpawnAfterInteractComponent, AfterInteractEvent>(HandleAfterInteract);
        }

        private async void HandleAfterInteract(EntityUid uid, SpawnAfterInteractComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach && !component.IgnoreDistance)
                return;
            if (string.IsNullOrEmpty(component.Prototype))
                return;

            var gridUid = _transform.GetGrid(args.ClickLocation);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                return;
            if (!_maps.TryGetTileRef(gridUid.Value, grid, args.ClickLocation, out var tileRef))
                return;

            bool IsTileClear()
            {
                return tileRef.Tile.IsEmpty == false && !_turfSystem.IsTileBlocked(tileRef, CollisionGroup.MobMask);
            }

            if (!IsTileClear())
            {
                _popup.PopupCursor(Loc.GetString("spawn-after-interact-tile-blocked"), args.User);
                return;
            }

            if (component.DoAfterTime > 0)
            {
                var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfterTime, new AwaitedDoAfterEvent(), null)
                {
                    BreakOnMove = true,
                };
                var result = await _doAfterSystem.WaitDoAfter(doAfterArgs);

                if (result != DoAfterStatus.Finished)
                    return;
            }

            if (component.Deleted || !IsTileClear())
            {
                if (!component.Deleted && !IsTileClear())
                    _popup.PopupCursor(Loc.GetString("spawn-after-interact-tile-blocked"), args.User);
                return;
            }

            if (TryComp(uid, out StackComponent? stackComp)
                && component.RemoveOnInteract && !_stackSystem.Use(uid, 1, stackComp))
            {
                return;
            }

            var spawned = Spawn(component.Prototype, args.ClickLocation.SnapToGrid(grid));
            var ev = new SpawnAfterInteractSpawnedEvent(args.User, uid);
            RaiseLocalEvent(spawned, ref ev);

            if (component.RemoveOnInteract && stackComp == null)
                QueueDel(uid); // Frontier: TryQueueDel<QueueDel
        }
    }
}
