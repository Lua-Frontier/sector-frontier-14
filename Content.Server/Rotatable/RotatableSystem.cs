using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Rotatable;
using Content.Shared.Verbs;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Server.Rotatable
{
    /// <summary>
    ///     Handles verbs for the <see cref="RotatableComponent"/> and <see cref="FlippableComponent"/> components.
    /// </summary>
    public sealed class RotatableSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
        [Dependency] private readonly SharedInteractionSystem _interaction = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<FlippableComponent, GetVerbsEvent<Verb>>(AddFlipVerb);
            SubscribeLocalEvent<RotatableComponent, GetVerbsEvent<Verb>>(AddRotateVerbs);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.RotateObjectClockwise, new PointerInputCmdHandler(HandleRotateObjectClockwise))
                .Bind(ContentKeyFunctions.RotateObjectCounterclockwise, new PointerInputCmdHandler(HandleRotateObjectCounterclockwise))
                .Bind(ContentKeyFunctions.FlipObject, new PointerInputCmdHandler(HandleFlipObject))
                .Register<RotatableSystem>();
        }

        private void AddFlipVerb(EntityUid uid, FlippableComponent component, GetVerbsEvent<Verb> args)
        {
            if (!args.CanAccess
                || !args.CanInteract
                || !args.CanComplexInteract)
                return;

            // Check if the object is anchored.
            if (TryComp(uid, out PhysicsComponent? physics) && physics.BodyType == BodyType.Static)
                return;

            Verb verb = new()
            {
                Act = () => Flip(uid, component),
                Text = Loc.GetString("flippable-verb-get-data-text"),
                Category = VerbCategory.Rotate,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/flip.svg.192dpi.png")),
                Priority = -3, // show flip last
                DoContactInteraction = true
            };
            args.Verbs.Add(verb);
        }

        private void AddRotateVerbs(EntityUid uid, RotatableComponent component, GetVerbsEvent<Verb> args)
        {
            if (!args.CanAccess
                || !args.CanInteract
                || !args.CanComplexInteract
                || Transform(uid).NoLocalRotation) // Good ol prototype inheritance, eh?
                return;

            // Check if the object is anchored, and whether we are still allowed to rotate it.
            if (!component.RotateWhileAnchored &&
                TryComp(uid, out PhysicsComponent? physics) &&
                physics.BodyType == BodyType.Static)
                return;

            Verb resetRotation = new()
            {
                DoContactInteraction = true,
                Act = () => Rotate(uid, component, Angle.Zero), // Lua
                Category = VerbCategory.Rotate,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
                Text = "Reset",
                Priority = -2, // show CCW, then CW, then reset
                CloseMenu = false,
            };
            args.Verbs.Add(resetRotation);

            // rotate clockwise
            Verb rotateCW = new()
            {
                Act = () => Rotate(uid, component, Transform(uid).LocalRotation - component.Increment), // Lua
                Category = VerbCategory.Rotate,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png")),
                Priority = -1,
                CloseMenu = false, // allow for easy double rotations.
            };
            args.Verbs.Add(rotateCW);

            // rotate counter-clockwise
            Verb rotateCCW = new()
            {
                Act = () => Rotate(uid, component, Transform(uid).LocalRotation + component.Increment), // Lua
                Category = VerbCategory.Rotate,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rotate_ccw.svg.192dpi.png")),
                Priority = 0,
                CloseMenu = false, // allow for easy double rotations.
            };
            args.Verbs.Add(rotateCCW);
        }

        // Lua start
        private void Rotate(EntityUid uid, RotatableComponent component, Angle rotation)
        {
            var xform = Transform(uid);
            if (component.Pivot == System.Numerics.Vector2.Zero)
            {
                xform.LocalRotation = rotation;
                return;
            }

            var anchored = xform.Anchored;
            if (anchored)
                _transform.Unanchor(uid, xform);

            var oldPos = xform.LocalPosition;
            var pos = oldPos + xform.LocalRotation.RotateVec(component.Pivot) - rotation.RotateVec(component.Pivot);
            _transform.SetLocalPositionRotation(uid, pos, rotation, xform);

            if (!anchored || _transform.AnchorEntity(uid, xform))
                return;

            _transform.SetLocalPositionRotation(uid, oldPos, rotation, xform);
            _transform.AnchorEntity(uid, xform);
        }
        // Lua end

        /// <summary>
        ///     Replace a flippable entity with it's flipped / mirror-symmetric entity.
        /// </summary>
        public void Flip(EntityUid uid, FlippableComponent component)
        {
            var oldTransform = Comp<TransformComponent>(uid);
            var entity = Spawn(component.MirrorEntity, oldTransform.Coordinates);
            var newTransform = Comp<TransformComponent>(entity);
            newTransform.LocalRotation = oldTransform.LocalRotation;
            _transform.Unanchor(entity, newTransform);
            Del(uid);
        }

        public bool HandleRotateObjectClockwise(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
        {
            if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
                return false;

            if (!TryComp<RotatableComponent>(entity, out var rotatableComp))
                return false;

            if (!_actionBlocker.CanInteract(player, entity)
                || !_actionBlocker.CanComplexInteract(player)
                || !_interaction.InRangeAndAccessible(player, entity))
                return false;

            // Check if the object is anchored, and whether we are still allowed to rotate it.
            if (!rotatableComp.RotateWhileAnchored && TryComp(entity, out PhysicsComponent? physics) &&
                physics.BodyType == BodyType.Static)
            {
                _popup.PopupEntity(Loc.GetString("rotatable-component-try-rotate-stuck"), entity, player);
                return false;
            }

            Rotate(entity, rotatableComp, Transform(entity).LocalRotation - rotatableComp.Increment); // Lua
            return true;
        }

        public bool HandleRotateObjectCounterclockwise(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
        {
            if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
                return false;

            if (!TryComp<RotatableComponent>(entity, out var rotatableComp))
                return false;

            if (!_actionBlocker.CanInteract(player, entity)
                || !_actionBlocker.CanComplexInteract(player)
                || !_interaction.InRangeAndAccessible(player, entity))
                return false;

            // Check if the object is anchored, and whether we are still allowed to rotate it.
            if (!rotatableComp.RotateWhileAnchored && TryComp(entity, out PhysicsComponent? physics) &&
                physics.BodyType == BodyType.Static)
            {
                _popup.PopupEntity(Loc.GetString("rotatable-component-try-rotate-stuck"), entity, player);
                return false;
            }

            Rotate(entity, rotatableComp, Transform(entity).LocalRotation + rotatableComp.Increment); // Lua
            return true;
        }

        public bool HandleFlipObject(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
        {
            if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
                return false;

            if (!TryComp<FlippableComponent>(entity, out var flippableComp))
                return false;

            if (!_actionBlocker.CanInteract(player, entity)
                || !_actionBlocker.CanComplexInteract(player)
                || !_interaction.InRangeAndAccessible(player, entity))
                return false;

            // Check if the object is anchored.
            if (TryComp(entity, out PhysicsComponent? physics) && physics.BodyType == BodyType.Static)
            {
                _popup.PopupEntity(Loc.GetString("flippable-component-try-flip-is-stuck"), entity, player);
                return false;
            }

            Flip(entity, flippableComp);
            return true;
        }
    }
}
