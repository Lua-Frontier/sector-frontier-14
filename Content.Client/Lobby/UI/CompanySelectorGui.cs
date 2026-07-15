using Content.Shared._Mono.Company;
using Content.Shared.Preferences;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;

namespace Content.Client.Lobby.UI;

public sealed partial class CompanySelectorGui : BoxContainer
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public HumanoidCharacterProfile? Profile { get; private set; }
    public int? CharacterSlot { get; private set; }
    public bool IsDirty { get; private set; }

    public event Action<HumanoidCharacterProfile, int>? Save;

    private readonly Label _title;
    private readonly BoxContainer _companyList;
    private readonly RichTextLabel _selectionState;
    private readonly RichTextLabel _companyName;
    private readonly TextureRect _companyIcon;
    private readonly RichTextLabel _companyDescription;
    private readonly Button _confirmButton;
    private ButtonGroup _companyButtonGroup = new(false);

    public CompanySelectorGui()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 12;
        HorizontalExpand = true;
        VerticalExpand = true;
        Margin = new Thickness(8);

        var listPanel = new PanelContainer
        {
            MinSize = new Vector2(280, 0),
            VerticalExpand = true,
        };

        _companyList = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        var listLayout = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
        };

        listLayout.AddChild(new Label
        {
            Text = Loc.GetString("character-setup-gui-company-selector-list-title"),
            StyleClasses = { "LabelHeading" },
        });

        listLayout.AddChild(new Label
        {
            Text = Loc.GetString("character-setup-gui-company-selector-list-subtitle"),
        });

        var listScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        listScroll.AddChild(_companyList);
        listLayout.AddChild(listScroll);
        listPanel.AddChild(listLayout);

        var infoPanel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var infoLayout = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Margin = new Thickness(12),
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _title = new Label
        {
            Text = Loc.GetString("character-setup-gui-company-selector-title"),
            StyleClasses = { "LabelHeadingBigger" },
        };

        var subtitle = new Label
        {
            Text = Loc.GetString("character-setup-gui-company-selector-subtitle"),
        };

        _selectionState = new RichTextLabel();
        _companyName = new RichTextLabel();
        _companyIcon = new TextureRect
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MinSize = new Vector2(96, 96),
            MaxSize = new Vector2(96, 96),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            Visible = false,
        };
        _companyDescription = new RichTextLabel
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _confirmButton = new Button
        {
            Text = Loc.GetString("character-setup-gui-company-selector-confirm"),
            Disabled = true,
            MinSize = new Vector2(0, 36),
        };
        _confirmButton.OnPressed += _ => ConfirmSelection();

        infoLayout.AddChild(_title);
        infoLayout.AddChild(subtitle);
        infoLayout.AddChild(_selectionState);
        infoLayout.AddChild(_companyName);
        infoLayout.AddChild(_companyIcon);
        infoLayout.AddChild(_companyDescription);
        infoLayout.AddChild(new Control { VerticalExpand = true });
        infoLayout.AddChild(_confirmButton);
        infoPanel.AddChild(infoLayout);

        AddChild(listPanel);
        AddChild(infoPanel);
    }

    public void SetProfile(HumanoidCharacterProfile? profile, int? slot)
    {
        Profile = profile?.Clone();
        CharacterSlot = slot;
        IsDirty = false;
        RebuildCompanyButtons();
        UpdateCompanyInfo();
    }

    private void ConfirmSelection()
    {
        if (Profile == null || CharacterSlot == null)
            return;

        if (string.IsNullOrWhiteSpace(Profile.Company) || string.Equals(Profile.Company, "None", StringComparison.OrdinalIgnoreCase))
            return;

        IsDirty = false;
        Save?.Invoke(Profile, CharacterSlot.Value);
    }

    private void RebuildCompanyButtons()
    {
        _companyList.RemoveAllChildren();
        _companyButtonGroup = new ButtonGroup(false);

        foreach (var company in GetSelectableCompanies())
        {
            var button = new ContainerButton
            {
                HorizontalExpand = true,
            };
            button.AddStyleClass(ContainerButton.StyleClassButton);
            button.Group = _companyButtonGroup;

            var layout = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = 8,
            };

            if (TryGetCompanyIcon(company, out var texture))
            {
                layout.AddChild(new TextureRect
                {
                    Texture = texture,
                    MinSize = new Vector2(24, 24),
                    MaxSize = new Vector2(24, 24),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    VerticalAlignment = VAlignment.Center,
                });
            }

            layout.AddChild(new Label
            {
                Text = company.Name,
                HorizontalExpand = true,
                FontColorOverride = company.Color,
                VerticalAlignment = VAlignment.Center,
            });

            button.AddChild(layout);

            button.OnPressed += _ =>
            {
                if (Profile == null)
                    return;

                Profile = Profile.WithCompany(company.ID);
                IsDirty = true;
                UpdateCompanyInfo();
                RebuildCompanyButtons();
            };

            if (string.Equals(Profile?.Company, company.ID, StringComparison.OrdinalIgnoreCase))
                button.Pressed = true;

            _companyList.AddChild(button);
        }
    }

    private bool TryGetCompanyIcon(CompanyPrototype company, out Texture? texture)
    {
        texture = null;

        if (string.IsNullOrWhiteSpace(company.IconPath))
            return false;

        texture = _resourceCache.GetResource<TextureResource>(company.IconPath!).Texture;
        return true;
    }

    private void UpdateCompanyInfo()
    {
        if (Profile == null || string.IsNullOrWhiteSpace(Profile.Company) || string.Equals(Profile.Company, "None", StringComparison.OrdinalIgnoreCase))
        {
            _selectionState.SetMessage(FormattedMessage.FromMarkupPermissive($"[color=gray]{Loc.GetString("character-setup-gui-company-selector-step") }[/color]"));
            _companyName.SetMessage(FormattedMessage.FromMarkupPermissive($"[color=gray]{Loc.GetString("character-setup-gui-company-selector-empty") }[/color]"));
            _companyIcon.Visible = false;
            _companyIcon.Texture = null;
            _companyDescription.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("character-setup-gui-company-selector-empty-description")));
            _confirmButton.Disabled = true;
            return;
        }

        if (!_prototypeManager.TryIndex<CompanyPrototype>(Profile.Company, out var company))
        {
            _selectionState.SetMessage(FormattedMessage.FromMarkupPermissive($"[color=yellow]{Loc.GetString("character-setup-gui-company-selector-unknown-title") }[/color]"));
            _companyName.SetMessage(FormattedMessage.FromMarkupPermissive($"[color=yellow]{Profile.Company}[/color]"));
            _companyIcon.Visible = false;
            _companyIcon.Texture = null;
            _companyDescription.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("character-setup-gui-company-selector-unknown-description")));
            _confirmButton.Disabled = true;
            return;
        }

        _selectionState.SetMessage(FormattedMessage.FromMarkupPermissive($"[color=lightgreen]{Loc.GetString("character-setup-gui-company-selector-ready") }[/color]"));
        _companyName.SetMessage(FormattedMessage.FromMarkupPermissive($"[font size=16][color={company.Color.ToHex()}]{company.Name}[/color][/font]"));
        _companyIcon.Visible = TryGetCompanyIcon(company, out var iconTexture);
        _companyIcon.Texture = iconTexture;

        var description = !string.IsNullOrEmpty(company.Description) && Loc.TryGetString(company.Description, out var localized)
            ? localized
            : company.Description ?? "Описание отсутствует.";
        _companyDescription.SetMessage(FormattedMessage.FromMarkupPermissive($"{description}\n\n[color=gray]{Loc.GetString("character-setup-gui-company-selector-confirm-note") }[/color]"));
        _confirmButton.Disabled = false;
    }

    private List<CompanyPrototype> GetSelectableCompanies()
    {
        var username = _playerManager.LocalSession?.Name;
        return _prototypeManager.EnumeratePrototypes<CompanyPrototype>()
            .Where(company => company.ID != "None")
            .Where(company => !company.Disabled || username != null && company.Logins.Contains(username))
            .OrderByDescending(company => company.ID == Profile?.Company)
            .ThenBy(company => company.Name, StringComparer.Ordinal)
            .ToList();
    }
}
