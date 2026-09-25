using System.Linq;
using System.Numerics;
using Content.Client.Lua.Research;
using Content.Lua.UIKit.Styles;
using Content.Client.Lathe;
using Content.Client.Materials.UI;
using Content.Lua.Shared.Research.Discovery;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Lua.Research.Discovery;

[UsedImplicitly]
public sealed class DiscoveryJournalBoundUserInterface : BoundUserInterface
{
    private DiscoveryJournalWindow? _window;

    public DiscoveryJournalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DiscoveryJournalWindow>();
        _window.SetOwner(Owner);
        _window.PrintPressed += recipe => SendMessage(new DiscoveryPrintBlueprintMessage(recipe));
        _window.ServerPressed += () => SendMessage(new Content.Shared.Research.Components.ConsoleServerSelectionMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DiscoveryJournalBoundUserInterfaceState s)
            _window?.UpdateState(s);
    }
}

public sealed class DiscoveryJournalWindow : LunaWindow
{
    public event Action<string>? PrintPressed;
    public event Action? ServerPressed;

    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private readonly IEntityManager _entMan = IoCManager.Resolve<IEntityManager>();
    private readonly SpriteSystem _sprite;
    private readonly LatheSystem _lathe;
    private readonly RichTextLabel _pointsLabel;
    private readonly RichTextLabel _metaLabel;
    private readonly PanelContainer _serverStatusDot;
    private readonly LineEdit _searchEdit;
    private readonly BoxContainer _disciplineBar;
    private readonly BoxContainer _techList;
    private readonly EntityPrototypeView _techIcon;
    private readonly Label _techName;
    private readonly RichTextLabel _techMeta;
    private readonly BoxContainer _recipeList;
    private readonly Button _print;
    private readonly ProgressBar _printProgress;
    private readonly Label _printProgressLabel;
    private readonly PanelContainer _detailAccent;
    private readonly Label _unlocksHeader;
    private readonly MaterialStorageControl _materials;

    private DiscoveryJournalBoundUserInterfaceState? _state;
    private string? _disciplineFilter;
    private string _searchQuery = string.Empty;
    private int _selectedIndex = -1;
    private string? _selectedRecipe;
    private bool _printStyled;

    public DiscoveryJournalWindow()
    {
        _sprite = _entMan.System<SpriteSystem>();
        _lathe = _entMan.System<LatheSystem>();

        Title = Loc.GetString("discovery-research-journal-title");
        MinSize = SetSize = new Vector2(960, 620);
        ApplyLunaChrome();

        var shell = new PanelContainer
        {
            PanelOverride = LunaWindowStyle.Shell(),
            Margin = new Thickness(4, 2, 4, 4),
        };
        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10, 8, 10, 10),
        };

        var header = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 12 };
        _pointsLabel = new RichTextLabel { VerticalAlignment = VAlignment.Center };
        _metaLabel = new RichTextLabel { VerticalAlignment = VAlignment.Center };
        var (server, serverDot) = ResearchServerStatusDot.CreateSelectButton(
            Loc.GetString("discovery-research-server-button"),
            minHeight: 28,
            minWidth: 150);
        server.OnPressed += _ => ServerPressed?.Invoke();
        _serverStatusDot = serverDot;
        header.AddChild(new Control { HorizontalExpand = true });
        header.AddChild(_pointsLabel);
        header.AddChild(_metaLabel);
        header.AddChild(server);
        root.AddChild(header);

        var headerDiv = new PanelContainer { MinHeight = 1, MaxHeight = 1 };
        LunaWindowStyle.StyleDivider(headerDiv);
        root.AddChild(headerDiv);

        _searchEdit = new LineEdit
        {
            PlaceHolder = Loc.GetString("discovery-research-journal-search"),
            HorizontalExpand = true,
            MinHeight = 28,
        };
        _searchEdit.OnTextChanged += args =>
        {
            _searchQuery = args.Text.Trim();
            _selectedIndex = -1;
            RebuildTechList();
            ClearDetail();
        };
        root.AddChild(_searchEdit);

        _disciplineBar = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 4 };
        root.AddChild(new ScrollContainer
        {
            HScrollEnabled = true,
            VScrollEnabled = false,
            SetHeight = 36,
            Children = { _disciplineBar },
        });

        var body = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            VerticalExpand = true,
        };

        var listPanel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = LunaWindowStyle.Panel(),
        };
        _techList = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 3, Margin = new Thickness(6) };
        listPanel.AddChild(new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            Children = { _techList },
        });
        body.AddChild(listPanel);

        var detail = new PanelContainer
        {
            SetWidth = 340,
            VerticalExpand = true,
            PanelOverride = LunaWindowStyle.Panel(),
        };
        var detailCol = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10),
        };
        _detailAccent = new PanelContainer
        {
            MinHeight = 3,
            PanelOverride = new StyleBoxFlat { BackgroundColor = LunaWindowStyle.Accent },
        };
        var iconRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 10 };
        _techIcon = new EntityPrototypeView { Scale = new Vector2(2f, 2f), SetSize = new Vector2(56, 56) };
        var titleCol = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        _techName = new Label { MaxWidth = 260 };
        LunaWindowStyle.StyleHeading(_techName);
        _techMeta = new RichTextLabel { MaxWidth = 260 };
        titleCol.AddChild(_techName);
        titleCol.AddChild(_techMeta);
        iconRow.AddChild(_techIcon);
        iconRow.AddChild(titleCol);

        _unlocksHeader = new Label { Text = Loc.GetString("discovery-research-journal-unlocks") };
        LunaWindowStyle.StyleSecondary(_unlocksHeader);
        _recipeList = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2, VerticalExpand = true };
        _print = new Button
        {
            Text = Loc.GetString("discovery-research-print-blueprint-button"),
            MinHeight = 34,
            Disabled = true,
        };
        _print.OnPressed += _ =>
        {
            if (!string.IsNullOrEmpty(_selectedRecipe))
                PrintPressed?.Invoke(_selectedRecipe);
        };
        LunaWindowStyle.ApplyCompactStyle(_print);

        _printProgress = new ProgressBar();
        LunaWindowStyle.StyleProgressCooldown(_printProgress, 14f);
        _printProgressLabel = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleSecondary(_printProgressLabel);

        var materialsHeader = new Label
        {
            Text = Loc.GetString("lathe-menu-materials-title"),
            HorizontalAlignment = HAlignment.Center,
        };
        LunaWindowStyle.StyleSecondary(materialsHeader);
        _materials = new MaterialStorageControl
        {
            SetHeight = 110,
            VerticalExpand = false,
        };

        detailCol.AddChild(_detailAccent);
        detailCol.AddChild(iconRow);
        detailCol.AddChild(_unlocksHeader);
        detailCol.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _recipeList },
        });
        detailCol.AddChild(_print);
        detailCol.AddChild(_printProgress);
        detailCol.AddChild(_printProgressLabel);
        detailCol.AddChild(materialsHeader);
        detailCol.AddChild(_materials);
        detail.AddChild(detailCol);
        body.AddChild(detail);

        root.AddChild(body);

        shell.AddChild(root);
        ContentsContainer.AddChild(shell);
        ClearDetail();
    }

    public void SetOwner(EntityUid owner)
    {
        _materials.SetOwner(owner);
    }

    public void UpdateState(DiscoveryJournalBoundUserInterfaceState state)
    {
        _state = state;
        _pointsLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={LunaWindowStyle.Accent.ToHex()}]{Loc.GetString("discovery-research-journal-points", ("points", state.Points))}[/color]"));
        _metaLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={LunaWindowStyle.TextSecondary.ToHex()}]{Loc.GetString("discovery-research-journal-meta", ("faction", DiscoveryUiLoc.FactionName(state.Faction, _prototypes)), ("count", state.Unlocked.Count), ("compute", state.WorkingComputeHint))}[/color]"));
        ResearchServerStatusDot.Set(_serverStatusDot, state.HasServer);

        if (state.Printing != _printStyled)
        {
            _printStyled = state.Printing;
            if (state.Printing)
                LunaWindowStyle.StyleProgressGood(_printProgress, 14f);
            else
                LunaWindowStyle.StyleProgressCooldown(_printProgress, 14f);
        }

        var fraction = state.Printing && state.PrintDuration > 0f
            ? Math.Clamp(state.PrintProgress / state.PrintDuration, 0f, 1f)
            : 0f;
        _printProgress.MinValue = 0f;
        _printProgress.MaxValue = 1f;
        _printProgress.Value = fraction;
        _printProgressLabel.Text = state.Printing
            ? Loc.GetString("discovery-research-blueprint-print-progress", ("percent", (fraction * 100f).ToString("0")))
            : string.Empty;
        _print.Disabled = state.Printing || string.IsNullOrEmpty(_selectedRecipe);

        RebuildDisciplineBar();
        RebuildTechList();
        if (_selectedIndex >= 0 && _selectedIndex < state.Unlocked.Count)
            ShowDetail(_selectedIndex);
        else
            ClearDetail();
    }

    private void RebuildDisciplineBar()
    {
        _disciplineBar.RemoveAllChildren();
        if (_state == null)
            return;

        AddDisciplineChip(null, Loc.GetString("discovery-research-journal-all"), LunaWindowStyle.Accent, null);
        foreach (var group in _state.Unlocked.GroupBy(e => e.Discipline).OrderBy(g => g.Key))
        {
            var sample = group.First();
            AddDisciplineChip(sample.Discipline, sample.DisciplineName ?? sample.Discipline, sample.DisciplineColor, sample.Discipline);
        }
    }

    private void AddDisciplineChip(string? id, string title, Color color, string? disciplineProtoId)
    {
        var selected = _disciplineFilter == id;
        var btn = new Button
        {
            MinSize = new Vector2(28, 28),
            MaxSize = new Vector2(28, 28),
            ToggleMode = true,
            Pressed = selected,
            ToolTip = title,
            StyleClasses = { "ButtonSquare" },
            ModulateSelfOverride = selected ? color : Color.InterpolateBetween(color, Color.Black, 0.4f),
        };

        if (disciplineProtoId != null && _prototypes.TryIndex<TechDisciplinePrototype>(disciplineProtoId, out var disc))
        {
            btn.AddChild(new TextureRect
            {
                Texture = _sprite.Frame0(disc.Icon),
                TextureScale = new Vector2(2f, 2f),
                SetSize = new Vector2(16, 16),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Stretch = TextureRect.StretchMode.KeepCentered,
            });
        }
        else
        {
            btn.AddChild(new TextureRect
            {
                Texture = _sprite.Frame0(new SpriteSpecifier.Rsi(
                    new ResPath("_Lua/Interface/Misc/research_disciplines.rsi"), "all")),
                TextureScale = new Vector2(2f, 2f),
                SetSize = new Vector2(16, 16),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Stretch = TextureRect.StretchMode.KeepCentered,
            });
        }

        btn.OnPressed += _ =>
        {
            _disciplineFilter = id;
            _selectedIndex = -1;
            RebuildDisciplineBar();
            RebuildTechList();
            ClearDetail();
        };
        _disciplineBar.AddChild(btn);
    }

    private void RebuildTechList()
    {
        _techList.RemoveAllChildren();
        if (_state == null)
            return;

        var any = false;
        var query = _searchQuery;
        for (var i = 0; i < _state.Unlocked.Count; i++)
        {
            var entry = _state.Unlocked[i];
            if (_disciplineFilter != null && entry.Discipline != _disciplineFilter)
                continue;
            if (!string.IsNullOrEmpty(query) &&
                !entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !(entry.DisciplineName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !entry.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            any = true;
            var index = i;
            var row = BuildTechRow(entry, index == _selectedIndex);
            row.MouseFilter = MouseFilterMode.Stop;
            row.OnKeyBindDown += args =>
            {
                if (args.Function != EngineKeyFunctions.UIClick)
                    return;
                _selectedIndex = index;
                RebuildTechList();
                ShowDetail(index);
                args.Handle();
            };
            _techList.AddChild(row);
        }

        if (!any)
        {
            var empty = new Label
            {
                Text = Loc.GetString("discovery-research-journal-empty"),
                HorizontalAlignment = HAlignment.Center,
            };
            LunaWindowStyle.StyleMuted(empty);
            _techList.AddChild(empty);
        }
    }

    private PanelContainer BuildTechRow(DiscoveryUnlockedEntry entry, bool selected)
    {
        var wash = Color.InterpolateBetween(entry.DisciplineColor, LunaWindowStyle.PanelBg, 0.72f);
        var border = selected ? entry.DisciplineColor : LunaWindowStyle.PanelBorder;
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            MinHeight = 54,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = wash,
                BorderColor = border,
                BorderThickness = new Thickness(selected ? 2 : 1),
            },
            Margin = new Thickness(0, 1),
        };

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(6, 4),
            MouseFilter = MouseFilterMode.Ignore,
        };

        row.AddChild(new PanelContainer
        {
            SetWidth = 4,
            MinHeight = 42,
            PanelOverride = new StyleBoxFlat { BackgroundColor = entry.DisciplineColor },
            MouseFilter = MouseFilterMode.Ignore,
        });

        var icon = new EntityPrototypeView
        {
            Scale = new Vector2(1.4f, 1.4f),
            SetSize = new Vector2(36, 36),
            MouseFilter = MouseFilterMode.Ignore,
        };
        if (!string.IsNullOrEmpty(entry.EntityIcon) && _prototypes.HasIndex<EntityPrototype>(entry.EntityIcon))
            icon.SetPrototype(entry.EntityIcon);
        row.AddChild(icon);

        var textCol = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };
        var nameLabel = new Label
        {
            Text = entry.IsSignature ? $"★ {entry.Name}" : entry.Name,
            ClipText = true,
            MouseFilter = MouseFilterMode.Ignore,
        };
        LunaWindowStyle.StyleTiny(nameLabel);
        nameLabel.FontColorOverride = LunaWindowStyle.TextPrimary;
        var meta = new RichTextLabel { MouseFilter = MouseFilterMode.Ignore };
        meta.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={entry.DisciplineColor.ToHex()}]{entry.DisciplineName ?? entry.Discipline}[/color]  [color={LunaWindowStyle.TextMuted.ToHex()}]{entry.Cost} | {entry.RecipeIds.Count}[/color]"));
        textCol.AddChild(nameLabel);
        textCol.AddChild(meta);
        row.AddChild(textCol);

        panel.AddChild(row);
        return panel;
    }

    private void ShowDetail(int index)
    {
        if (_state == null || index < 0 || index >= _state.Unlocked.Count)
        {
            ClearDetail();
            return;
        }

        var entry = _state.Unlocked[index];
        _detailAccent.PanelOverride = new StyleBoxFlat { BackgroundColor = entry.DisciplineColor };
        _techName.Text = entry.IsSignature
            ? Loc.GetString("discovery-research-journal-signature", ("name", entry.Name))
            : entry.Name;
        _techMeta.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={entry.DisciplineColor.ToHex()}]{entry.DisciplineName ?? entry.Discipline}[/color]\n" +
            $"[color={LunaWindowStyle.TextSecondary.ToHex()}]{Loc.GetString("discovery-research-journal-detail-meta", ("cost", entry.Cost))}[/color]"));

        if (!string.IsNullOrEmpty(entry.EntityIcon) && _prototypes.HasIndex<EntityPrototype>(entry.EntityIcon))
            _techIcon.SetPrototype(entry.EntityIcon);
        else
            _techIcon.SetPrototype(null);

        _recipeList.RemoveAllChildren();
        _selectedRecipe = null;
        _print.Disabled = true;

        for (var i = 0; i < entry.RecipeIds.Count; i++)
        {
            var recipeId = entry.RecipeIds[i];
            var recipeName = ResolveRecipeName(recipeId, i < entry.RecipeNames.Count ? entry.RecipeNames[i] : null);
            string? resultProto = null;
            if (_prototypes.TryIndex<LatheRecipePrototype>(recipeId, out var recipe) && recipe.Result is { } result)
                resultProto = result;

            var recipeBtn = new Button
            {
                HorizontalExpand = true,
                MinHeight = 36,
                ToggleMode = true,
                ToolTip = recipeId,
            };
            LunaWindowStyle.ApplyCompactStyle(recipeBtn);

            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                Margin = new Thickness(4, 2),
            };

            var recipeIcon = new EntityPrototypeView
            {
                Scale = new Vector2(1.15f, 1.15f),
                SetSize = new Vector2(28, 28),
            };
            if (resultProto != null && _prototypes.HasIndex<EntityPrototype>(resultProto))
                recipeIcon.SetPrototype(resultProto);
            row.AddChild(recipeIcon);

            var nameLabel = new Label
            {
                Text = recipeName,
                HorizontalExpand = true,
                ClipText = true,
                VerticalAlignment = VAlignment.Center,
            };
            LunaWindowStyle.StyleTiny(nameLabel);
            nameLabel.FontColorOverride = LunaWindowStyle.TextPrimary;
            row.AddChild(nameLabel);
            recipeBtn.AddChild(row);

            recipeBtn.OnPressed += _ =>
            {
                foreach (var child in _recipeList.Children)
                {
                    if (child is Button other)
                        other.Pressed = other == recipeBtn;
                }

                _selectedRecipe = recipeId;
                _print.Disabled = _state?.Printing == true;
            };
            _recipeList.AddChild(recipeBtn);
        }

        if (entry.RecipeIds.Count == 0)
        {
            var none = new Label { Text = Loc.GetString("discovery-research-journal-no-recipes") };
            LunaWindowStyle.StyleMuted(none);
            _recipeList.AddChild(none);
        }
    }

    private string ResolveRecipeName(string recipeId, string? serverName)
    {
        if (!string.IsNullOrWhiteSpace(serverName))
            return serverName;

        if (_prototypes.TryIndex<LatheRecipePrototype>(recipeId, out var recipe))
        {
            var name = _lathe.GetRecipeName(recipe);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return recipeId;
    }

    private void ClearDetail()
    {
        _techName.Text = Loc.GetString("discovery-research-journal-select");
        _techMeta.SetMessage(FormattedMessage.FromMarkupOrThrow($"[color={LunaWindowStyle.TextMuted.ToHex()}]-[/color]"));
        _techIcon.SetPrototype(null);
        _recipeList.RemoveAllChildren();
        _selectedRecipe = null;
        _print.Disabled = true;
        _detailAccent.PanelOverride = new StyleBoxFlat { BackgroundColor = LunaWindowStyle.Accent };
    }
}
