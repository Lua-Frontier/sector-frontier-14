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
using System.Numerics;

namespace Content.Client._Lua.Stargate;

public sealed class StargateAddressEditorWindow : DefaultWindow
{
    public event Action<byte>? OnSymbolPressed;
    public event Action? OnClear;
    public event Action? OnSaveToLeft;
    public event Action? OnSaveToRight;
    public event Action<int>? OnDeleteFromLeft;
    public event Action<int>? OnDeleteFromRight;
    public event Action<int>? OnMoveLeftToRight;
    public event Action<int>? OnMoveRightToLeft;
    public event Action<int>? OnCopyLeftToRight;
    public event Action<int>? OnCopyRightToLeft;
    public event Action? OnCloneLeftToRight;
    public event Action? OnCloneRightToLeft;

    private Label AddressDisplay = default!;
    private LayoutContainer DhdRingContainer = default!;
    private Button ClearButton = default!;

    private PanelContainer LeftDiskPanel = default!;
    private Button SaveToLeftButton = default!;
    private BoxContainer LeftDiskAddressList = default!;

    private PanelContainer RightDiskPanel = default!;
    private Button SaveToRightButton = default!;
    private BoxContainer RightDiskAddressList = default!;

    private Font? _glyphFont;
    private Font? _glyphFontSmall;
    private byte _maxSymbols = 40;
    private byte[] _currentInput = Array.Empty<byte>();
    private byte[][]? _leftDiskAddresses;
    private byte[][]? _rightDiskAddresses;

    private readonly Dictionary<byte, (ContainerButton Btn, Label Lbl)> _symbolButtons = new();

    private const float RingRadius = 160f;
    private const float BtnSize = 26f;
    private const float ContainerSize = 400f;

    private static readonly Color GlyphNormal = Color.FromHex("#D4C5A0");
    private static readonly Color GlyphHover = Color.FromHex("#3FB2FF");
    private static readonly Color GlyphDisabled = Color.FromHex("#555555");
    private static readonly Color GlyphActive = Color.FromHex("#1C5CFF");

    public StargateAddressEditorWindow()
    {
        RobustXamlLoader.Load(this);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        var cache = IoCManager.Resolve<IResourceCache>();
        _glyphFont = cache.GetFont("/Fonts/StarGate/stargatesg1addressglyphs.ttf", 18);
        _glyphFontSmall = cache.GetFont("/Fonts/StarGate/stargatesg1addressglyphs.ttf", 14);

        AddressDisplay = FindControl<Label>("AddressDisplay");
        DhdRingContainer = FindControl<LayoutContainer>("DhdRingContainer");
        ClearButton = FindControl<Button>("ClearButton");

        LeftDiskPanel = FindControl<PanelContainer>("LeftDiskPanel");
        SaveToLeftButton = FindControl<Button>("SaveToLeftButton");
        LeftDiskAddressList = FindControl<BoxContainer>("LeftDiskAddressList");

        RightDiskPanel = FindControl<PanelContainer>("RightDiskPanel");
        SaveToRightButton = FindControl<Button>("SaveToRightButton");
        RightDiskAddressList = FindControl<BoxContainer>("RightDiskAddressList");

        if (_glyphFontSmall != null)
            AddressDisplay.FontOverride = _glyphFontSmall;

        ClearButton.OnPressed += _ => OnClear?.Invoke();
        SaveToLeftButton.OnPressed += _ => OnSaveToLeft?.Invoke();
        SaveToRightButton.OnPressed += _ => OnSaveToRight?.Invoke();

        RebuildSymbolRing(_maxSymbols);
    }

    private int GetRequiredLen()
    {
        if (_currentInput.Length > 0 && _currentInput[0] == 1)
            return 7;
        return 6;
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

            btn.OnPressed += _ => OnSymbolPressed?.Invoke(symbol);

            _symbolButtons[symbol] = (btn, label);
            DhdRingContainer.AddChild(btn);

            var angle = 2.0 * Math.PI * (i - 1) / symbolCount - Math.PI / 2.0;
            var x = center + (float)(RingRadius * Math.Cos(angle)) - BtnSize / 2f;
            var y = center + (float)(RingRadius * Math.Sin(angle)) - BtnSize / 2f;

            LayoutContainer.SetPosition(btn, new Vector2(x, y));
        }
    }

    public void UpdateState(StargateAddressEditorUiState state)
    {
        _currentInput = state.CurrentInput;
        _leftDiskAddresses = state.LeftDiskAddresses;
        _rightDiskAddresses = state.RightDiskAddresses;

        if (state.MaxSymbols != _maxSymbols)
        {
            _maxSymbols = state.MaxSymbols;
            RebuildSymbolRing(_maxSymbols);
        }

        UpdateAddressDisplay();
        UpdateButtons();
        UpdateDiskPanels();
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

    private void UpdateButtons()
    {
        var requiredLen = GetRequiredLen();
        var inputFull = _currentInput.Length >= requiredLen;

        var hasLeftDisk = _leftDiskAddresses != null;
        var hasRightDisk = _rightDiskAddresses != null;
        SaveToLeftButton.Disabled = !hasLeftDisk || _currentInput.Length < requiredLen;
        SaveToRightButton.Disabled = !hasRightDisk || _currentInput.Length < requiredLen;

        foreach (var (sym, entry) in _symbolButtons)
        {
            var isUsed = _currentInput.Contains(sym);
            var disabled = isUsed || inputFull;
            entry.Btn.Disabled = disabled;

            if (disabled)
                entry.Lbl.FontColorOverride = isUsed ? GlyphActive : GlyphDisabled;
            else
                entry.Lbl.FontColorOverride = GlyphNormal;
        }
    }

    private void UpdateDiskPanels()
    {
        var hasLeft = _leftDiskAddresses != null;
        var hasRight = _rightDiskAddresses != null;
        LeftDiskPanel.Visible = true;
        RightDiskPanel.Visible = true;
        SaveToLeftButton.Visible = hasLeft;
        SaveToRightButton.Visible = hasRight;

        LeftDiskAddressList.RemoveAllChildren();
        RightDiskAddressList.RemoveAllChildren();

        if (hasLeft && _leftDiskAddresses!.Length > 0)
        {
            var cloneBtn = new Button
            {
                Text = Loc.GetString("stargate-editor-clone-to-right"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4),
            };
            cloneBtn.OnPressed += _ => OnCloneLeftToRight?.Invoke();
            LeftDiskAddressList.AddChild(cloneBtn);
        }

        for (var i = 0; i < (_leftDiskAddresses?.Length ?? 0); i++)
        {
            var address = _leftDiskAddresses![i];
            var index = i;
            LeftDiskAddressList.AddChild(CreateAddressRow(address, false, index));
        }

        if (hasRight && _rightDiskAddresses!.Length > 0)
        {
            var cloneBtn = new Button
            {
                Text = Loc.GetString("stargate-editor-clone-to-left"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4),
            };
            cloneBtn.OnPressed += _ => OnCloneRightToLeft?.Invoke();
            RightDiskAddressList.AddChild(cloneBtn);
        }

        for (var i = 0; i < (_rightDiskAddresses?.Length ?? 0); i++)
        {
            var address = _rightDiskAddresses![i];
            var index = i;
            RightDiskAddressList.AddChild(CreateAddressRow(address, true, index));
        }
    }

    private BoxContainer CreateAddressRow(byte[] address, bool isRightDisk, int index)
    {
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
            MinWidth = 90,
        };

        if (_glyphFontSmall != null)
            addressLabel.FontOverride = _glyphFontSmall;

        var parts = new string[address.Length];
        for (var j = 0; j < address.Length; j++)
            parts[j] = StargateGlyphs.GetChar(address[j]).ToString();
        addressLabel.Text = string.Join(" ", parts);

        row.AddChild(addressLabel);

        if (isRightDisk)
        {
            var moveBtn = new Button { Text = "<-", MinWidth = 28, Margin = new Thickness(2, 0, 0, 0) };
            moveBtn.OnPressed += _ => OnMoveRightToLeft?.Invoke(index);
            row.AddChild(moveBtn);

            var copyBtn = new Button { Text = "<= ", MinWidth = 28, Margin = new Thickness(2, 0, 0, 0) };
            copyBtn.OnPressed += _ => OnCopyRightToLeft?.Invoke(index);
            row.AddChild(copyBtn);
        }
        else
        {
            var moveBtn = new Button { Text = "->", MinWidth = 28, Margin = new Thickness(2, 0, 0, 0) };
            moveBtn.OnPressed += _ => OnMoveLeftToRight?.Invoke(index);
            row.AddChild(moveBtn);

            var copyBtn = new Button { Text = " =>", MinWidth = 28, Margin = new Thickness(2, 0, 0, 0) };
            copyBtn.OnPressed += _ => OnCopyLeftToRight?.Invoke(index);
            row.AddChild(copyBtn);
        }

        var deleteBtn = new Button { Text = "X", MinWidth = 24, Margin = new Thickness(2, 0, 0, 0) };
        deleteBtn.OnPressed += _ =>
        {
            if (isRightDisk)
                OnDeleteFromRight?.Invoke(index);
            else
                OnDeleteFromLeft?.Invoke(index);
        };
        row.AddChild(deleteBtn);

        return row;
    }
}
