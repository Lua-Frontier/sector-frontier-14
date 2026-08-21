// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client._Lua.Styles;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.ViewVariables;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Content.Client._Lua.UserInterface.Systems.Sandbox;

public sealed class SpawnAndVvWindowPatchSystem : EntitySystem
{
    private const string CategoryFilterRowName = "LuaCategoryFilterRow";

    private static readonly HashSet<string> HiddenCategoryFilters = new(StringComparer.Ordinal)
    {
        "Mapping",
        "Debug",
        //"Erp",
        "ForkFiltered",
    };

    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly HashSet<EntitySpawnWindow> _patchedSpawnWindows = new();
    private readonly HashSet<DefaultWindow> _patchedVvWindows = new();

    public override void Initialize()
    {
        base.Initialize();
        _ui.WindowRoot.OnChildAdded += OnWindowAdded;
    }

    public override void Shutdown()
    {
        _ui.WindowRoot.OnChildAdded -= OnWindowAdded;
        base.Shutdown();
    }

    private void OnWindowAdded(Control control)
    {
        if (control is EntitySpawnWindow spawnWindow)
        {
            PatchEntitySpawnWindow(spawnWindow);
            return;
        }

        if (control is ViewVariablesAddWindow addWindow)
        {
            PatchVvAddWindow(addWindow);
            return;
        }

        if (control is DefaultWindow vvWindow &&
            string.Equals(vvWindow.Title, Loc.GetString("view-variables"), StringComparison.Ordinal))
        {
            PatchVvWindow(vvWindow);
        }
    }

    private void PatchVvWindow(DefaultWindow window)
    {
        window.SetSize = new Vector2(900, 560);
        window.MinSize = new Vector2(700, 360);
        LunaWindowStyle.ApplyWindowChrome(window);
        window.Contents.Margin = new Thickness(6, 4, 6, 6);

        if (!_patchedVvWindows.Add(window))
            return;

        LunaWindowStyle.ApplyCompactStyle(window.Contents);
        window.Contents.OnChildAdded += StyleVvContentChild;
        foreach (var child in window.Contents.Children)
            StyleVvContentChild(child);
    }

    private void PatchVvAddWindow(ViewVariablesAddWindow window)
    {
        window.SetSize = new Vector2(360, 420);
        window.MinSize = new Vector2(300, 260);
        LunaWindowStyle.ApplyWindowChrome(window);
        window.Contents.Margin = new Thickness(6, 4, 6, 6);

        if (!_patchedVvWindows.Add(window))
            return;

        LunaWindowStyle.ApplyCompactStyle(window.Contents);
        var addButton = window.FindControl<Button>("AddButton");
        StyleWindowButton(addButton, LunaWindowStyle.AccentGood);
    }

    private static void StyleVvContentChild(Control child)
    {
        switch (child)
        {
            case Label label:
                LunaWindowStyle.StyleSecondary(label);
                break;
            case Button button:
                StyleWindowButton(button, LunaWindowStyle.Accent);
                break;
            case OptionButton option:
                StyleOptionButton(option);
                break;
            default:
                LunaWindowStyle.ApplyCompactStyle(child);
                break;
        }
    }

    private void PatchEntitySpawnWindow(EntitySpawnWindow window)
    {
        window.SetSize = new Vector2(460, 420);
        window.MinSize = new Vector2(420, 220);
        LunaWindowStyle.ApplyWindowChrome(window);
        window.Contents.Margin = new Thickness(6, 4, 6, 6);

        var root = window.Contents.GetChild(0) as BoxContainer;
        if (root == null)
            return;

        if (_patchedSpawnWindows.Contains(window))
        {
            RemoveExtraCategoryRows(root, keep: 1);
            return;
        }

        RemoveExtraCategoryRows(root, keep: 0);

        var searchBar = window.FindControl<LineEdit>("SearchBar");
        var prototypeScroll = window.FindControl<ScrollContainer>("PrototypeScrollContainer");
        var prototypeList = window.FindControl<Control>("PrototypeList");
        var clearButton = window.FindControl<Button>("ClearButton");
        var replaceButton = window.FindControl<Button>("ReplaceButton");
        var eraseButton = window.FindControl<Button>("EraseButton");
        var overrideMenu = window.FindControl<OptionButton>("OverrideMenu");
        var rotationLabel = window.FindControl<Label>("RotationLabel");

        var categoryLabel = new Label
        {
            Text = Loc.GetString("entity-spawn-window-category-filter-label"),
            VerticalAlignment = Control.VAlignment.Center
        };

        var categoryFilter = new OptionButton
        {
            HorizontalExpand = true
        };

        var categoryRow = new BoxContainer
        {
            Name = CategoryFilterRowName,
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Children =
            {
                categoryLabel,
                categoryFilter
            }
        };

        var insertIndex = -1;
        for (var i = 0; i < root.ChildCount; i++)
        {
            if (ReferenceEquals(root.GetChild(i), prototypeScroll))
            {
                insertIndex = i;
                break;
            }
        }
        if (insertIndex < 0)
            insertIndex = 1;
        root.AddChild(categoryRow);
        categoryRow.SetPositionInParent(insertIndex);

        var filters = BuildCategoryOptions(categoryFilter);

        categoryFilter.OnItemSelected += args =>
        {
            if (args.Id < 0 || args.Id >= filters.Count)
                return;

            categoryFilter.SelectId(args.Id);
            _cfg.SetCVar(CVars.EntitiesCategoryFilter, filters[args.Id]);
            RefreshEntityList(searchBar);
        };

        if (!string.IsNullOrEmpty(_cfg.GetCVar(CVars.EntitiesCategoryFilter)))
            RefreshEntityList(searchBar);

        ApplyLunaInterior(window, categoryLabel, categoryFilter, clearButton, replaceButton, eraseButton, overrideMenu, rotationLabel);
        if (prototypeList != null)
            prototypeList.OnChildAdded += StyleSpawnListItem;

        _patchedSpawnWindows.Add(window);
    }

    private static void ApplyLunaInterior(
        EntitySpawnWindow window,
        Label categoryLabel,
        OptionButton categoryFilter,
        Button? clearButton,
        Button? replaceButton,
        Button? eraseButton,
        OptionButton? overrideMenu,
        Label? rotationLabel)
    {
        LunaWindowStyle.ApplyCompactStyle(window.Contents);
        LunaWindowStyle.StyleHeading(categoryLabel);
        StyleOptionButton(categoryFilter);
        StyleOptionButton(overrideMenu);
        StyleWindowButton(clearButton, LunaWindowStyle.TextPrimary);
        StyleWindowButton(replaceButton, LunaWindowStyle.Accent);
        StyleWindowButton(eraseButton, LunaWindowStyle.AccentBad);
        if (rotationLabel != null)
            LunaWindowStyle.StyleMuted(rotationLabel);
    }

    private static void StyleWindowButton(Button? button, Color color)
    {
        if (button == null)
            return;

        button.AddStyleClass(StyleNano.StyleClassButtonNavCompact);
        if (button.Label == null)
            return;

        button.Label.FontOverride = LunaWindowStyle.FontSmall;
        button.Label.FontColorOverride = color;
    }

    private static void StyleOptionButton(OptionButton? button)
    {
        if (button == null)
            return;

        button.AddStyleClass(StyleNano.StyleClassButtonNavCompact);
    }

    private static void StyleSpawnListItem(Control child)
    {
        if (child is not EntitySpawnButton button)
            return;

        button.ActualButton.AddStyleClass(StyleNano.StyleClassButtonNavCompact);
        LunaWindowStyle.StyleSecondary(button.EntityLabel);
    }

    private static void RemoveExtraCategoryRows(BoxContainer root, int keep)
    {
        var kept = 0;
        for (var i = root.ChildCount - 1; i >= 0; i--)
        {
            if (root.GetChild(i).Name != CategoryFilterRowName)
                continue;

            if (kept >= keep)
                root.RemoveChild(root.GetChild(i));
            else
                kept++;
        }
    }

    private static void RefreshEntityList(LineEdit searchBar)
    {
        var text = searchBar.Text ?? string.Empty;
        searchBar.SetText(text + "\u200b", invokeEvent: false);
        searchBar.SetText(text, invokeEvent: true);
    }

    private List<string> BuildCategoryOptions(OptionButton categoryFilter)
    {
        var filters = new List<string> { string.Empty };
        categoryFilter.AddItem(Loc.GetString("entity-spawn-window-category-filter-all"), 0);

        var categories = _prototypes.EnumeratePrototypes<EntityCategoryPrototype>()
            .Where(c => !c.HideSpawnMenu && !HiddenCategoryFilters.Contains(c.ID))
            .Select(c => new
            {
                c.ID,
                Name = string.IsNullOrEmpty(c.Name) ? c.ID : Loc.GetString(c.Name)
            })
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var index = 1;
        foreach (var category in categories)
        {
            categoryFilter.AddItem(category.Name, index++);
            filters.Add(category.ID);
        }

        var currentFilter = _cfg.GetCVar(CVars.EntitiesCategoryFilter);
        var selectedIndex = filters.FindIndex(f => string.Equals(f, currentFilter, StringComparison.Ordinal));
        categoryFilter.SelectId(selectedIndex >= 0 ? selectedIndex : 0);

        return filters;
    }
}

