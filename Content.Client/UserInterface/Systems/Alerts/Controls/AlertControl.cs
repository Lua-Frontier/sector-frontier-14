using System.Numerics;
using Content.Client.Actions.UI;
using Content.Client.Cooldown;
using Content.Shared.Alert;
using Content.Shared.Lua.CLVar;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Alerts.Controls
{
    public sealed class AlertControl : BaseButton
    {
        private static readonly Vector2 BaseAlertIconMaxSize = new(48, 48);

        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        private readonly SpriteSystem _sprite;

        public AlertPrototype Alert { get; }

        /// <summary>
        /// Current cooldown displayed in this slot. Set to null to show no cooldown.
        /// </summary>
        public (TimeSpan Start, TimeSpan End)? Cooldown
        {
            get => _cooldown;
            set
            {
                _cooldown = value;
                if (SuppliedTooltip is ActionAlertTooltip actionAlertTooltip)
                {
                    actionAlertTooltip.Cooldown = value;
                }
            }
        }

        private (TimeSpan Start, TimeSpan End)? _cooldown;

        private short? _severity;

        private readonly SpriteView _icon;
        private readonly CooldownGraphic _cooldownGraphic;

        private EntityUid _spriteViewEntity;
        private bool _iconSetupPending;

        /// <summary>
        /// Creates an alert control reflecting the indicated alert + state
        /// </summary>
        /// <param name="alert">alert to display</param>
        /// <param name="severity">severity of alert, null if alert doesn't have severity levels</param>
        public AlertControl(AlertPrototype alert, short? severity)
        {
            // Alerts will handle this.
            MuteSounds = true;

            IoCManager.InjectDependencies(this);
            _sprite = _entityManager.System<SpriteSystem>();
            TooltipSupplier = SupplyTooltip;
            Alert = alert;

            HorizontalAlignment = HAlignment.Left;
            _severity = severity;
            _icon = new SpriteView
            {
                Stretch = SpriteView.StretchMode.None,
                HorizontalAlignment = HAlignment.Left
            };

            _cooldownGraphic = new CooldownGraphic();
            SetIconScale(_cfg.GetCVar(CLVars.AlertsIconScale));

            SetupIcon();

            Children.Add(_icon);
            Children.Add(_cooldownGraphic);
        }

        public void SetIconScale(float scale)
        {
            scale = Math.Clamp(scale, 0.1f, 10f);
            _icon.Scale = new Vector2(scale, scale);
            var maxSize = BaseAlertIconMaxSize * scale;
            _icon.MaxSize = maxSize;
            _cooldownGraphic.MaxSize = maxSize;
        }

        private Control SupplyTooltip(Control? sender)
        {
            var msg = FormattedMessage.FromMarkupOrThrow(Loc.GetString(Alert.Name));
            var desc = FormattedMessage.FromMarkupOrThrow(Loc.GetString(Alert.Description));
            return new ActionAlertTooltip(msg, desc) { Cooldown = Cooldown };
        }

        /// <summary>
        /// Change the alert severity, changing the displayed icon
        /// </summary>
        public void SetSeverity(short? severity)
        {
            if (_severity == severity)
                return;
            _severity = severity;

            if (!_entityManager.TryGetComponent<SpriteComponent>(_spriteViewEntity, out var sprite))
                return;
            var icon = Alert.GetIcon(_severity);
            if (_sprite.LayerMapTryGet((_spriteViewEntity, sprite), AlertVisualLayers.Base, out var layer, false))
                _sprite.LayerSetSprite((_spriteViewEntity, sprite), layer, icon);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (_iconSetupPending && !_timing.ApplyingState) //costyl
            {
                _iconSetupPending = false;
                SetupIconImmediate();
            }

            UserInterfaceManager.GetUIController<AlertsUIController>().UpdateAlertSpriteEntity(_spriteViewEntity, Alert);

            if (!Cooldown.HasValue)
            {
                _cooldownGraphic.Visible = false;
                _cooldownGraphic.Progress = 0;
                return;
            }

            _cooldownGraphic.FromTime(Cooldown.Value.Start, Cooldown.Value.End);
        }

        private void SetupIcon() //costyl
        {
            if (_timing.ApplyingState)
            {
                _iconSetupPending = true;
                return;
            }
            _iconSetupPending = false;
            SetupIconImmediate();
        }

        private void SetupIconImmediate()
        {
            if (!_entityManager.Deleted(_spriteViewEntity)) _entityManager.QueueDeleteEntity(_spriteViewEntity);

            _spriteViewEntity = _entityManager.Spawn(Alert.AlertViewEntity);
            if (_entityManager.TryGetComponent<SpriteComponent>(_spriteViewEntity, out var sprite))
            {
                var icon = Alert.GetIcon(_severity);
                if (_sprite.LayerMapTryGet((_spriteViewEntity, sprite), AlertVisualLayers.Base, out var layer, false))
                    _sprite.LayerSetSprite((_spriteViewEntity, sprite), layer, icon);
            }

            _icon.SetEntity(_spriteViewEntity);
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();
            SetupIcon();
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();

            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);
        }
    }

    public enum AlertVisualLayers : byte
    {
        Base
    }
}
