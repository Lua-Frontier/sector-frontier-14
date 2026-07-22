// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Client.Eui;
using Content.Client.UserInterface.Controls;
using Content.Shared._Lua.Company;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Lua.Company;

[UsedImplicitly]
public sealed class CompanyBriefingEui : BaseEui
{
    private readonly CompanyBriefingWindow _window;

    public CompanyBriefingEui()
    {
        _window = new CompanyBriefingWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        base.Opened();
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is not CompanyBriefingEuiState briefing)
            return;

        _window.UpdateState(briefing);
    }

    private sealed class CompanyBriefingWindow : FancyWindow
    {
        private readonly Label _titleLabel;
        private readonly PanelContainer _bodyPanel;
        private readonly ScrollContainer _bodyScroll;
        private readonly RichTextLabel _bodyLabel;
        private readonly Button _closeButton;

        private string _fullText = string.Empty;
        private float _charactersShown;

        public CompanyBriefingWindow()
        {
            Title = Loc.GetString("company-briefing-popup-window-title");
            MinSize = SetSize = new Vector2(900f, 520f);

            var framePanel = (PanelContainer) GetChild(0);
            var windowLayout = (BoxContainer) GetChild(1);
            var titleHost = windowLayout.GetChild(0);
            var titlePanel = (PanelContainer) titleHost.GetChild(0);
            var dividerPanel = (PanelContainer) windowLayout.GetChild(1);

            framePanel.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                BorderColor = Color.Transparent,
                BorderThickness = new Thickness(0),
            };

            titlePanel.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#10151D0D"),
                BorderColor = Color.Transparent,
                BorderThickness = new Thickness(0),
            };

            dividerPanel.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#5D6F8720"),
                BorderColor = Color.Transparent,
                BorderThickness = new Thickness(0),
                ContentMarginTopOverride = 1,
                ContentMarginBottomOverride = 1,
            };

            var root = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 12,
                Margin = new Thickness(10),
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            ContentsContainer.AddChild(root);

            _titleLabel = new Label
            {
                HorizontalExpand = true,
                FontColorOverride = Color.White,
                Align = Label.AlignMode.Center,
            };
            root.AddChild(_titleLabel);

            _bodyPanel = new PanelContainer
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#10151D12"),
                    BorderColor = Color.FromHex("#5D6F8740"),
                    BorderThickness = new Thickness(2),
                    ContentMarginLeftOverride = 10,
                    ContentMarginTopOverride = 10,
                    ContentMarginRightOverride = 10,
                    ContentMarginBottomOverride = 10,
                },
            };
            root.AddChild(_bodyPanel);

            _bodyScroll = new ScrollContainer
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                HScrollEnabled = false,
            };
            _bodyPanel.AddChild(_bodyScroll);

            _bodyLabel = new RichTextLabel
            {
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            _bodyScroll.AddChild(_bodyLabel);

            _closeButton = new Button
            {
                Text = Loc.GetString("company-briefing-popup-close"),
                HorizontalAlignment = HAlignment.Right,
                Disabled = true,
            };
            _closeButton.OnPressed += _ => Close();
            root.AddChild(_closeButton);
        }

        public void UpdateState(CompanyBriefingEuiState state)
        {
            _titleLabel.Text = state.Title;
            _titleLabel.FontColorOverride = state.Color;
            _fullText = state.Text;
            _charactersShown = 0f;
            _closeButton.Disabled = true;
            _bodyScroll.SetScrollValue(Vector2.Zero);
            UpdateVisibleText();
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (!_closeButton.Disabled)
                return;

            _charactersShown = MathF.Min(_fullText.Length, _charactersShown + args.DeltaSeconds * 45f);
            UpdateVisibleText();

            if (_charactersShown >= _fullText.Length)
                _closeButton.Disabled = false;
        }

        private void UpdateVisibleText()
        {
            var count = Math.Clamp((int) _charactersShown, 0, _fullText.Length);
            _bodyLabel.SetMessage(FormattedMessage.FromUnformatted(_fullText[..count]));
        }
    }
}
