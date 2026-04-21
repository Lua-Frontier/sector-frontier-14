// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Resources;
using Content.Shared._Lua.Stargate;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Lua.Stargate;

public sealed class StargateConsoleWindow : DefaultWindow
{
    public event Action<byte>? OnSymbolPressed;
    public event Action<byte[]>? OnDial;
    public event Action<byte[]>? OnAutoDialFromDisk;
    public event Action? OnClear;
    public event Action? OnClosePortal;
    public event Action? OnSaveDiskAddress;
    public event Action<int>? OnDeleteDiskAddress;
    public event Action? OnToggleIris;

    private Label StatusLabel = default!;
    private Label GateAddressTitleLabel = default!;
    private Label GateAddressLabel = default!;
    private Label AddressDisplay = default!;
    private LayoutContainer DhdRingContainer = default!;
    private Button DialButton = default!;
    private Button ClearButton = default!;
    private Button ClosePortalButton = default!;
    private Button CopyAddressButton = default!;
    private Button IrisToggleButton = default!;

    private PanelContainer DiskPanel = default!;
    private Label DiskTitleLabel = default!;
    private Button SaveAddressButton = default!;
    private BoxContainer DiskAddressList = default!;

    private Texture? _lockTexture;
    private Texture? _unlockTexture;

    private Font? _glyphFont;
    private Font? _glyphFontSmall;
    private byte _maxSymbols = 40;
    private byte[] _currentInput = Array.Empty<byte>();
    private byte[] _gateAddress = Array.Empty<byte>();
    private byte[][]? _diskAddresses;
    private bool _portalOpen;
    private bool _dialing;
    private bool _autoDialing;
    private bool _hasControllable;
    private bool _irisOpen;

    private readonly Dictionary<byte, (ContainerButton Btn, Label Lbl)> _symbolButtons = new();

    private const float RingRadius = 210f;
    private const float BtnSize = 30f;
    private const float DialBtnSize = 70f;
    private const float ContainerSize = 490f;

    private static readonly Color GlyphNormal = Color.FromHex("#D4C5A0");
    private static readonly Color GlyphHover = Color.FromHex("#3FB2FF");
    private static readonly Color GlyphDisabled = Color.FromHex("#555555");
    private static readonly Color GlyphActive = Color.FromHex("#1C5CFF");
    private static readonly Color AddressButtonColor = Color.FromHex("#2a2a3a");

    public StargateConsoleWindow()
    {
        RobustXamlLoader.Load(this);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        var cache = IoCManager.Resolve<IResourceCache>();
        _glyphFont = cache.GetFont("/Fonts/StarGate/stargatesg1addressglyphs.ttf", 20);
        _glyphFontSmall = cache.GetFont("/Fonts/StarGate/stargatesg1addressglyphs.ttf", 16);

        StatusLabel = FindControl<Label>("StatusLabel");
        GateAddressTitleLabel = FindControl<Label>("GateAddressTitleLabel");
        GateAddressLabel = FindControl<Label>("GateAddressLabel");
        AddressDisplay = FindControl<Label>("AddressDisplay");
        DhdRingContainer = FindControl<LayoutContainer>("DhdRingContainer");
        ClearButton = FindControl<Button>("ClearButton");
        ClosePortalButton = FindControl<Button>("ClosePortalButton");
        CopyAddressButton = FindControl<Button>("CopyAddressButton");
        IrisToggleButton = FindControl<Button>("IrisToggleButton");

        _lockTexture = cache.GetResource<TextureResource>("/Textures/Interface/VerbIcons/lock.svg.192dpi.png").Texture;
        _unlockTexture = cache.GetResource<TextureResource>("/Textures/Interface/VerbIcons/unlock.svg.192dpi.png").Texture;

        DiskPanel = FindControl<PanelContainer>("DiskPanel");
        DiskTitleLabel = FindControl<Label>("DiskTitleLabel");
        SaveAddressButton = FindControl<Button>("SaveAddressButton");
        DiskAddressList = FindControl<BoxContainer>("DiskAddressList");

        if (_glyphFontSmall != null)
            AddressDisplay.FontOverride = _glyphFontSmall;

        if (_glyphFont != null)
            GateAddressLabel.FontOverride = _glyphFont;

        CreateDialButton();
        UpdateIrisButton();

        ClearButton.OnPressed += _ => OnClear?.Invoke();

        ClosePortalButton.OnPressed += _ => OnClosePortal?.Invoke();
        IrisToggleButton.OnPressed += _ => OnToggleIris?.Invoke();

        CopyAddressButton.OnPressed += _ =>
        {
            if (_gateAddress.Length == 0)
                return;

            var glyphs = StargateGlyphs.ToGlyphString(_gateAddress);
            var markup = $"[stargate size=20]{string.Join(" ", glyphs.ToCharArray())}[/stargate]";
            IoCManager.Resolve<IClipboardManager>().SetText(markup);
        };

        SaveAddressButton.OnPressed += _ => OnSaveDiskAddress?.Invoke();

        RebuildSymbolRing(_maxSymbols);
    }

    private void CreateDialButton()
    {
        DialButton = new Button
        {
            Text = Loc.GetString("stargate-console-dial"),
            MinWidth = DialBtnSize,
            MinHeight = DialBtnSize,
            MaxWidth = DialBtnSize,
            MaxHeight = DialBtnSize,
        };

        DhdRingContainer.AddChild(DialButton);

        var center = ContainerSize / 2f;
        LayoutContainer.SetPosition(DialButton, new Vector2(center - DialBtnSize / 2f, center - DialBtnSize / 2f));

        DialButton.OnPressed += _ =>
        {
            if (_currentInput.Length > 0)
                OnDial?.Invoke(_currentInput);
        };
    }

    private void RebuildSymbolRing(byte symbolCount)
    {
        foreach (var (_, entry) in _symbolButtons)
        {
            DhdRingContainer.RemoveChild(entry.Btn);
        }
        _symbolButtons.Clear();

        var center = ContainerSize / 2f;
        var transparent = new StyleBoxFlat(Color.Transparent);

        for (byte i = 1; i <= symbolCount; i++)
        {
            var symbol = i;
            var glyphChar = StargateGlyphs.GetChar(symbol);

            var label = new Label
            {
                Text = glyphChar.ToString(),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                FontColorOverride = GlyphNormal,
            };

            if (_glyphFont != null)
                label.FontOverride = _glyphFont;

            var btn = new ContainerButton
            {
                MinWidth = BtnSize,
                MinHeight = BtnSize,
                MaxWidth = BtnSize,
                MaxHeight = BtnSize,
                StyleBoxOverride = transparent,
            };

            btn.AddChild(label);

            btn.OnMouseEntered += _ =>
            {
                if (!btn.Disabled)
                    label.FontColorOverride = GlyphHover;
            };

            btn.OnMouseExited += _ =>
            {
                if (!btn.Disabled)
                    label.FontColorOverride = _currentInput.Contains(symbol) ? GlyphActive : GlyphNormal;
            };

            btn.OnPressed += _ =>
            {
                OnSymbolPressed?.Invoke(symbol);
            };

            _symbolButtons[symbol] = (btn, label);
            DhdRingContainer.AddChild(btn);

            var angle = 2.0 * Math.PI * (i - 1) / symbolCount - Math.PI / 2.0;
            var x = center + (float)(RingRadius * Math.Cos(angle)) - BtnSize / 2f;
            var y = center + (float)(RingRadius * Math.Sin(angle)) - BtnSize / 2f;

            LayoutContainer.SetPosition(btn, new Vector2(x, y));
        }
    }

    public void UpdateState(StargateConsoleUiState state)
    {
        _currentInput = state.CurrentInput;
        _portalOpen = state.PortalOpen;
        _gateAddress = state.GateAddress;
        _dialing = state.Dialing;
        _autoDialing = state.AutoDialing;
        _diskAddresses = state.DiskAddresses;
        _hasControllable = state.HasControllable;
        _irisOpen = state.IrisOpen;

        if (state.MaxSymbols != _maxSymbols)
        {
            _maxSymbols = state.MaxSymbols;
            RebuildSymbolRing(_maxSymbols);
        }

        UpdateGateAddressDisplay();
        UpdateAddressDisplay();
        UpdateButtons();
        UpdateStatus();
        UpdateDiskPanel();
        UpdateIrisButton();
    }

    private void UpdateGateAddressDisplay()
    {
        if (_gateAddress.Length == 0)
        {
            GateAddressTitleLabel.Visible = false;
            GateAddressLabel.Text = "";
            CopyAddressButton.Visible = false;
            return;
        }

        GateAddressTitleLabel.Visible = true;

        var parts = new string[_gateAddress.Length];
        for (var i = 0; i < _gateAddress.Length; i++)
        {
            parts[i] = StargateGlyphs.GetChar(_gateAddress[i]).ToString();
        }

        GateAddressLabel.Text = string.Join(" ", parts);
        CopyAddressButton.Visible = true;
    }

    private void UpdateAddressDisplay()
    {
        var requiredLen = GetRequiredLen();
        var parts = new string[requiredLen];

        for (var i = 0; i < requiredLen; i++)
        {
            parts[i] = i < _currentInput.Length
                ? StargateGlyphs.GetChar(_currentInput[i]).ToString()
                : "_";
        }

        AddressDisplay.Text = string.Join(" - ", parts);
    }

    private int GetRequiredLen()
    {
        if (_currentInput.Length > 0 && _currentInput[0] == 1)
            return 7;
        return 6;
    }

    private void UpdateButtons()
    {
        var requiredLen = GetRequiredLen();
        var inputFull = _currentInput.Length >= requiredLen;

        DialButton.Disabled = _currentInput.Length < requiredLen || _portalOpen || _dialing || _autoDialing;
        ClosePortalButton.Disabled = !_portalOpen && !_dialing;
        ClosePortalButton.Visible = _portalOpen || _dialing;
        foreach (var (sym, entry) in _symbolButtons)
        {
            var isUsed = _currentInput.Contains(sym);
            var disabled = isUsed || inputFull || _portalOpen || _dialing || _autoDialing;
            entry.Btn.Disabled = disabled;

            if (disabled)
                entry.Lbl.FontColorOverride = isUsed ? GlyphActive : GlyphDisabled;
            else
                entry.Lbl.FontColorOverride = GlyphNormal;
        }
    }

    private void UpdateStatus()
    {
        if (_autoDialing)
        {
            StatusLabel.Text = Loc.GetString("stargate-console-status-auto-dialing");
        }
        else if (_dialing)
        {
            StatusLabel.Text = Loc.GetString("stargate-console-status-dialing");
        }
        else if (_portalOpen)
        {
            StatusLabel.Text = Loc.GetString("stargate-console-status-active");
        }
        else if (_currentInput.Length > 0)
        {
            StatusLabel.Text = Loc.GetString("stargate-console-status-input");
        }
        else
        {
            StatusLabel.Text = Loc.GetString("stargate-console-status-idle");
        }
    }

    private void UpdateDiskPanel()
    {
        DiskPanel.Visible = _diskAddresses != null;
        SaveAddressButton.Visible = _diskAddresses != null && _gateAddress.Length > 0;

        DiskAddressList.RemoveAllChildren();

        if (_diskAddresses == null)
            return;

        for (var i = 0; i < _diskAddresses.Length; i++)
        {
            var address = _diskAddresses[i];
            var index = i;

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(0, 2, 0, 2),
            };

            var addressLabel = new Label
            {
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
            };

            if (_glyphFontSmall != null)
                addressLabel.FontOverride = _glyphFontSmall;

            var parts = new string[address.Length];
            for (var j = 0; j < address.Length; j++)
                parts[j] = StargateGlyphs.GetChar(address[j]).ToString();
            addressLabel.Text = string.Join(" ", parts);

            var dialBtn = new Button
            {
                Text = Loc.GetString("stargate-console-disk-dial"),
                MinWidth = 40,
                Margin = new Thickness(2, 0, 0, 0),
            };
            dialBtn.OnPressed += _ => OnAutoDialFromDisk?.Invoke(address);

            var deleteBtn = new Button
            {
                Text = "X",
                MinWidth = 28,
                Margin = new Thickness(2, 0, 0, 0),
            };
            deleteBtn.OnPressed += _ => OnDeleteDiskAddress?.Invoke(index);

            row.AddChild(addressLabel);
            row.AddChild(dialBtn);
            row.AddChild(deleteBtn);

            DiskAddressList.AddChild(row);
        }
    }

    private void UpdateIrisButton()
    {
        IrisToggleButton.Disabled = !_hasControllable;

        const float iconSize = 18f;
        if (_irisOpen)
        {
            if (_lockTexture != null)
            {
                IrisToggleButton.Children.Clear();
                var tex = new TextureRect
                {
                    Texture = _lockTexture,
                    MinSize = new Vector2(iconSize, iconSize),
                    SetSize = new Vector2(iconSize, iconSize),
                    Stretch = TextureRect.StretchMode.Scale
                };
                IrisToggleButton.AddChild(tex);
            }
            IrisToggleButton.ToolTip = Loc.GetString("stargate-console-iris-lock");
        }
        else
        {
            if (_unlockTexture != null)
            {
                IrisToggleButton.Children.Clear();
                var tex = new TextureRect
                {
                    Texture = _unlockTexture,
                    MinSize = new Vector2(iconSize, iconSize),
                    SetSize = new Vector2(iconSize, iconSize),
                    Stretch = TextureRect.StretchMode.Scale
                };
                IrisToggleButton.AddChild(tex);
            }
            IrisToggleButton.ToolTip = Loc.GetString("stargate-console-iris-unlock");
        }
    }
}
