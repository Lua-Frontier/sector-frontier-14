using System;
using Content.Client._Lua.Announce;
using Content.Shared._RMC14.Announce;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Announce;

public sealed partial class AnnouncementWidget
{
    private sealed class DecalBuilder
    {
        private readonly AnnouncementWidget _owner;

        public DecalBuilder(AnnouncementWidget owner)
        {
            _owner = owner;
        }

        private static ResPath NormalizeDecalRsiPath(string path)
        {
            var normalized = path.Trim().Replace('\\', '/');
            if (normalized.StartsWith("/Textures/", StringComparison.OrdinalIgnoreCase))
                return new ResPath(normalized);

            if (normalized.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase))
                return new ResPath($"/{normalized}");

            return new ResPath($"/Textures/{normalized.TrimStart('/')}");
        }

        private static string ToHolopadRsiPath(ResPath path)
        {
            var normalized = path.ToString().Trim().Replace('\\', '/');
            if (normalized.StartsWith("/Textures/", StringComparison.OrdinalIgnoreCase))
                return normalized["/Textures/".Length..];

            if (normalized.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase))
                return normalized["Textures/".Length..];

            return normalized.TrimStart('/');
        }

        public Control? CreatePortraitFlagContainer(AnnouncementDisplayData announcement, AnnouncementStyle style)
        {
            try
            {
                var resPath = NormalizeDecalRsiPath(announcement.DecalRsi!);
                var holopadRsiPath = ToHolopadRsiPath(resPath);
                var portraitSystem = _owner._entityManager.System<AnnouncementPortraitSystem>();
                var flagEntity = portraitSystem.CreateHologramFlag(holopadRsiPath, announcement.DecalState!);
                if (flagEntity == null)
                    return null;
                _owner.OwnPortraitHologram(flagEntity.Value);

                var clipContainer = new Control
                {
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Top,
                    RectClipContent = true,
                    HorizontalExpand = false,
                    VerticalExpand = false
                };

                var spriteView = new HolopadPortraitSpriteView(_owner._entityManager)
                {
                    HorizontalAlignment = HAlignment.Stretch,
                    VerticalAlignment = VAlignment.Stretch,
                    Stretch = SpriteView.StretchMode.Fill,
                    OverrideDirection = Direction.South
                };
                spriteView.SetEntity(flagEntity.Value);

                AnnouncementWidget.ApplyFixedPortraitFlagLayout(clipContainer, spriteView, style);
                AnnouncementWidget.ApplyPortraitTuning(spriteView, style);
                clipContainer.AddChild(spriteView);
                return clipContainer;
            }
            catch (Exception ex)
            {
                Logger.Error($"[AnnouncementWidget] Failed to load portrait flag {announcement.DecalRsi}:{announcement.DecalState}: {ex}");
                return null;
            }
        }

    }
}
