using Content.Shared.CrewManifest;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using System.Numerics;
using Content.Shared.Roles;

namespace Content.Client.CrewManifest.UI;

public sealed class CrewManifestSection : BoxContainer
{
    public CrewManifestSection(
        IPrototypeManager prototypeManager,
        SpriteSystem spriteSystem,
        DepartmentPrototype section,
        List<CrewManifestEntry> entries)
        : this(prototypeManager, spriteSystem, Loc.GetString(section.Name), entries, showPlayerNames: false)
    {
    }

    public CrewManifestSection(
        IPrototypeManager prototypeManager,
        SpriteSystem spriteSystem,
        string sectionTitle,
        List<CrewManifestEntry> entries,
        bool showPlayerNames)
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        AddChild(new Label()
        {
            StyleClasses = { "LabelBig" },
            Text = sectionTitle
        });

        var gridContainer = new GridContainer()
        {
            HorizontalExpand = true,
            Columns = 2
        };

        AddChild(gridContainer);

        foreach (var entry in entries)
        {
            var displayName = entry.Name;
            if (showPlayerNames && !string.IsNullOrWhiteSpace(entry.PlayerName))
                displayName = Loc.GetString("crew-manifest-name-with-player", ("name", entry.Name), ("player", entry.PlayerName));

            var name = new RichTextLabel()
            {
                HorizontalExpand = true,
            };
            name.SetMessage(displayName);

            var titleContainer = new BoxContainer()
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true
            };

            var title = new RichTextLabel();
            title.SetMessage(entry.JobTitle);


            if (prototypeManager.TryIndex<JobIconPrototype>(entry.JobIcon, out var jobIcon))
            {
                var icon = new TextureRect()
                {
                    TextureScale = new Vector2(2, 2),
                    VerticalAlignment = VAlignment.Center,
                    Texture = spriteSystem.Frame0(jobIcon.Icon),
                    Margin = new Thickness(0, 0, 4, 0)
                };

                titleContainer.AddChild(icon);
                titleContainer.AddChild(title);
            }
            else
            {
                titleContainer.AddChild(title);
            }

            gridContainer.AddChild(name);
            gridContainer.AddChild(titleContainer);
        }
    }
}
