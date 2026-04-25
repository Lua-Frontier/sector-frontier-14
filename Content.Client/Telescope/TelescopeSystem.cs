using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.CCVar;
using Content.Shared.Camera;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Telescope;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client.Telescope;

public sealed class TelescopeSystem : EntitySystem
{
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private ScalingViewport? _viewport;
    private bool _holdLookUp;
    private bool _toggled;
    private Vector2 _currentOffset = Vector2.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelescopeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);

        _cfg.OnValueChanged(CCVars.HoldLookUp,
            val =>
            {
                var input = val ? null : InputCmdHandler.FromDelegate(_ => _toggled = !_toggled);
                _input.SetInputCommand(ContentKeyFunctions.LookUp, input);
                _holdLookUp = val;
                _toggled = false;
                _currentOffset = Vector2.Zero;
            },
            true);
    }

    private void OnGetEyeOffset(EntityUid uid, TelescopeComponent component, ref GetEyeOffsetEvent args)
    {
        if (_player.LocalEntity != uid)
            return;

        args.Offset += _currentOffset;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_timing.ApplyingState
            || !_timing.IsFirstTimePredicted
            || !_input.MouseScreenPosition.IsValid)
            return;

        var player = _player.LocalEntity;
        var telescope = GetRightTelescope(player);

        if (player == null || telescope == null)
        {
            _toggled = false;
            _currentOffset = Vector2.Zero;
            return;
        }

        if (!TryComp<EyeComponent>(player.Value, out var eye))
        {
            _currentOffset = Vector2.Zero;
            return;
        }

        var targetOffset = Vector2.Zero;

        if (_holdLookUp)
        {
            if (_inputSystem.CmdStates.GetState(ContentKeyFunctions.LookUp) != BoundKeyState.Down)
            {
                _currentOffset = Vector2.Zero;
                return;
            }
        }
        else if (!_toggled)
        {
            _currentOffset = Vector2.Zero;
            return;
        }

        var mousePos = _input.MouseScreenPosition;

        if (_uiManager.MouseGetControl(mousePos) is ScalingViewport viewport)
            _viewport = viewport;

        if (_viewport == null)
            return;

        var centerPos = _eyeManager.WorldToScreen(eye.Eye.Position.Position + eye.Offset);

        var diff = mousePos.Position - centerPos;
        var len = diff.Length();

        var size = _viewport.PixelSize;
        var maxLength = Math.Min(size.X, size.Y) * 0.4f;
        var minLength = maxLength * 0.2f;

        if (len > maxLength)
        {
            diff *= maxLength / len;
            len = maxLength;
        }

        var divisor = maxLength * telescope.Divisor;

        if (len > minLength)
        {
            diff -= diff * minLength / len;
            targetOffset = new Vector2(diff.X / divisor, -diff.Y / divisor);
            targetOffset = new Angle(-eye.Rotation.Theta).RotateVec(targetOffset);
        }

        _currentOffset = Vector2.Lerp(_currentOffset, targetOffset, telescope.LerpAmount);
    }

    private TelescopeComponent? GetRightTelescope(EntityUid? entity)
    {
        if (entity == null)
            return null;

        if (TryComp(entity.Value, out HandsComponent? hands)
            && _hands.TryGetActiveItem((entity.Value, hands), out var held)
            && TryComp<TelescopeComponent>(held.Value, out var handTelescope))
        {
            return handTelescope;
        }

        return CompOrNull<TelescopeComponent>(entity.Value);
    }
}
