using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Inventory.Controls;

public sealed class InventoryDisplay : LayoutContainer
{
    private const int DefaultCellSize = 75;
    private const int DefaultButtonSpacing = 5;
    private const int DefaultMarginThickness = 10;

    public int CellSize { get; set; } = DefaultCellSize;
    public int ButtonSpacing { get; set; } = DefaultButtonSpacing;
    public int MarginThickness { get; set; } = DefaultMarginThickness;
    public bool InvertY { get; set; }

    private readonly Control _resizer;
    private readonly Dictionary<string, (SlotControl Control, Vector2 Offset, float Scale)> _buttons = new();

    public InventoryDisplay()
    {
        _resizer = new Control();
        AddChild(_resizer);
    }

    public SlotControl AddButton(SlotControl newButton, Vector2i buttonOffset)
    {
        return AddButton(newButton, new Vector2(buttonOffset.X, buttonOffset.Y));
    }

    public SlotControl AddButton(SlotControl newButton, Vector2 buttonOffset, float scale = 1f)
    {
        if (newButton.Parent != this)
            AddChild(newButton);

        HorizontalExpand = true;
        VerticalExpand = true;
        InheritChildMeasure = true;
        _buttons[newButton.SlotName] = (newButton, buttonOffset, scale);
        Relayout();
        return newButton;
    }

    public SlotControl? GetButton(string slotName)
    {
        return _buttons.TryGetValue(slotName, out var foundButton) ? foundButton.Control : null;
    }

    public bool TryGetButton(string slotName, out SlotControl? button)
    {
        var success = _buttons.TryGetValue(slotName, out var buttonData);
        button = buttonData.Control;
        return success;
    }

    public void RemoveButton(string slotName)
    {
        if (!_buttons.Remove(slotName, out var removed))
            return;

        if (removed.Control.Parent == this)
            RemoveChild(removed.Control);

        Relayout();
    }

    public void ClearButtons()
    {
        foreach (var (control, _, _) in _buttons.Values)
        {
            if (control.Parent == this)
                RemoveChild(control);
        }

        _buttons.Clear();
        Relayout();
    }

    public void Relayout()
    {
        var maxX = 0f;
        var maxY = 0f;
        foreach (var (_, offset, scale) in _buttons.Values)
        {
            maxX = Math.Max(maxX, offset.X + scale);
            maxY = Math.Max(maxY, offset.Y + scale);
        }

        foreach (var (control, offset, scale) in _buttons.Values)
        {
            var x = offset.X * CellSize + ButtonSpacing;
            var y = InvertY
                ? (maxY - offset.Y - scale) * CellSize + ButtonSpacing
                : offset.Y * CellSize + ButtonSpacing;
            SetPosition(control, new Vector2(x, y));
        }

        _resizer.SetHeight = maxY * CellSize + ButtonSpacing * 2 + MarginThickness;
        _resizer.SetWidth = maxX * CellSize + ButtonSpacing * 2 + MarginThickness;
    }
}
