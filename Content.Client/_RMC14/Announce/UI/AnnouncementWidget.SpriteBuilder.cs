using System.Numerics;
using Content.Client.Resources;
using Content.Client._Lua.Announce;
using Content.Client._RMC14.Announce.Styling;
using Content.Client.UserInterface.Controls;
using Content.Shared._RMC14.Announce;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Announce;

public sealed partial class AnnouncementWidget
{
    private sealed class SpriteBuilder
    {
        private readonly AnnouncementWidget _owner;
        private readonly DecalBuilder _decalBuilder;

        public SpriteBuilder(AnnouncementWidget owner, DecalBuilder decalBuilder)
        {
            _owner = owner;
            _decalBuilder = decalBuilder;
        }

        public Control? CreateSpriteContainer(AnnouncementDisplayData announcement, AnnouncementStyle style, Vector2 screenSize)
        {
            if (announcement.IsFactionFlagPortrait)
            {
                var flagContainer = _decalBuilder.CreatePortraitFlagContainer(announcement, style);
                if (flagContainer != null)
                    return WrapPortraitPresentation(flagContainer, style, screenSize);
            }

            if (!announcement.ShowSprite)
                return null;

            if (!string.IsNullOrWhiteSpace(announcement.PortraitPrototype))
                return CreatePrototypePortrait(announcement.PortraitPrototype, style, screenSize);

            if (!string.IsNullOrWhiteSpace(announcement.PortraitRsi) &&
                !string.IsNullOrWhiteSpace(announcement.PortraitState))
            {
                var tint = announcement.TintPortrait
                    ? style.TitleConfig.TitleColor
                    : Color.White;
                return CreateRsiPortrait(
                    announcement.PortraitRsi,
                    announcement.PortraitState,
                    tint,
                    announcement.TintPortrait,
                    style,
                    screenSize);
            }

            if (!announcement.SpeakerEntity.HasValue ||
                !_owner._entityManager.TryGetEntity(announcement.SpeakerEntity.Value, out var speakerUid))
            {
                if (_owner.PreviewMode)
                    return WrapWithCrtIfEnabled(CreatePreviewSpritePlaceholder(style, screenSize), style, screenSize);

                return null;
            }

            var spriteScale = style.SpriteConfig.SpriteScale;
            var screenScaleFactor = AnnouncementStyling.CalculateScreenScaleFactor(screenSize);

            var clipContainer = new Control
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Top,
                RectClipContent = true
            };

            var portraitSystem = _owner._entityManager.System<AnnouncementPortraitSystem>();
            var hologram = portraitSystem.CreateHologramPortrait(speakerUid.Value);
            var portraitEntity = hologram ?? speakerUid.Value;
            if (hologram is { } ownedHologram)
                _owner.OwnPortraitHologram(ownedHologram);

            var spriteView = new HolopadPortraitSpriteView(_owner._entityManager)
            {
                HorizontalAlignment = HAlignment.Stretch,
                VerticalAlignment = VAlignment.Stretch,
                Stretch = SpriteView.StretchMode.Fill
            };

            spriteView.SetEntity(portraitEntity);
            spriteView.OverrideDirection = Direction.South;

            _owner.SetSpriteDisplayProperties(clipContainer, spriteView, style, spriteScale, screenScaleFactor);
            AnnouncementWidget.ApplyPortraitTuning(spriteView, style);
            clipContainer.AddChild(spriteView);

            var container = WrapPortraitSpriteBox(clipContainer, style);
            container = WrapWithCrtIfEnabled(container, style, screenSize);

            if (style.TextConfig.ShowSpeakerName &&
                (!string.IsNullOrEmpty(announcement.SpeakerName) || !string.IsNullOrEmpty(announcement.SpeakerJobTitle)))
            {
                var spriteWithMask = container;
                spriteWithMask.Measure(screenSize);
                var speakerWidth = MathF.Max(1f, spriteWithMask.DesiredSize.X);

                var nameContainer = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Top,
                    SeparationOverride = CalculateSpeakerNameSeparation(style, screenScaleFactor)
                };

                if (style.LayoutConfig.SpeakerNamePosition == AnnouncementSpeakerNamePosition.Above)
                {
                    if (!string.IsNullOrEmpty(announcement.SpeakerJobTitle))
                        nameContainer.AddChild(CreateSpeakerLabel(announcement.SpeakerJobTitle, style, speakerWidth, screenScaleFactor, dimmed: true));
                    if (!string.IsNullOrEmpty(announcement.SpeakerName))
                        nameContainer.AddChild(CreateSpeakerLabel(announcement.SpeakerName, style, speakerWidth, screenScaleFactor));
                    nameContainer.AddChild(spriteWithMask);
                }
                else
                {
                    nameContainer.AddChild(spriteWithMask);
                    if (!string.IsNullOrEmpty(announcement.SpeakerName))
                        nameContainer.AddChild(CreateSpeakerLabel(announcement.SpeakerName, style, speakerWidth, screenScaleFactor));
                    if (!string.IsNullOrEmpty(announcement.SpeakerJobTitle))
                        nameContainer.AddChild(CreateSpeakerLabel(announcement.SpeakerJobTitle, style, speakerWidth, screenScaleFactor, dimmed: true));
                }

                container = nameContainer;
            }

            return container;
        }

        private Control CreatePrototypePortrait(string prototype, AnnouncementStyle style, Vector2 screenSize)
        {
            var clipSize = AnnouncementWidget.GetPortraitClipSize(style);
            var clipContainer = new Control
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Top,
                RectClipContent = true,
                MinWidth = clipSize.X,
                MinHeight = clipSize.Y,
                SetWidth = clipSize.X,
                SetHeight = clipSize.Y
            };

            var prototypeView = new HolopadPortraitSpriteView(_owner._entityManager, forceHolopadEffect: true)
            {
                HorizontalAlignment = HAlignment.Stretch,
                VerticalAlignment = VAlignment.Stretch,
                Stretch = SpriteView.StretchMode.Fill,
                Scale = new Vector2(3.2f * style.SpriteConfig.SpriteScale)
            };
            prototypeView.SetPrototype(prototype);
            AnnouncementWidget.ApplyPortraitTuning(prototypeView, style);
            clipContainer.AddChild(prototypeView);

            return WrapPortraitPresentation(clipContainer, style, screenSize);
        }

        private Control CreateRsiPortrait(
            string rsiPath,
            string rsiState,
            Color tint,
            bool applyTint,
            AnnouncementStyle style,
            Vector2 screenSize)
        {
            var clipSize = AnnouncementWidget.GetPortraitClipSize(style);
            var clipContainer = new Control
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Top,
                RectClipContent = true,
                MinWidth = clipSize.X,
                MinHeight = clipSize.Y,
                SetWidth = clipSize.X,
                SetHeight = clipSize.Y
            };

            var scale = 3f * style.SpriteConfig.SpriteScale;
            var sprite = new AnimatedTextureRect
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                HorizontalExpand = false,
                VerticalExpand = false,
                SetWidth = 32f * scale,
                SetHeight = 32f * scale
            };
            sprite.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(new ResPath(rsiPath), rsiState));
            sprite.DisplayRect.Stretch = TextureRect.StretchMode.Scale;
            sprite.DisplayRect.TextureScale = new Vector2(scale);
            if (applyTint && _owner._prototypeManager.TryIndex<ShaderPrototype>("LuaAnnouncementTint", out var shaderPrototype))
            {
                var shader = shaderPrototype.InstanceUnique();
                shader.SetParameter("tint_color", tint);
                sprite.DisplayRect.ShaderOverride = shader;
                sprite.DisplayRect.Modulate = Color.White;
            }
            else
            {
                sprite.DisplayRect.Modulate = tint;
            }
            AnnouncementWidget.ApplyPortraitTuning(sprite, style);
            clipContainer.AddChild(sprite);

            return WrapPortraitPresentation(clipContainer, style, screenSize);
        }

        private Label CreateSpeakerLabel(
            string text,
            AnnouncementStyle style,
            float minWidth,
            float screenScaleFactor,
            bool dimmed = false)
        {
            var label = new Label
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Align = Label.AlignMode.Center,
                VAlign = Label.VAlignMode.Center,
                Text = text,
                MinWidth = minWidth,
                FontColorOverride = dimmed
                    ? style.TextConfig.SpeakerNameColor.WithAlpha(0.75f)
                    : style.TextConfig.SpeakerNameColor
            };

            var fontSize = dimmed
                ? Math.Max(1, (int)MathF.Round(style.TextConfig.SpeakerNameFontSize * 0.9f))
                : Math.Max(1, (int)MathF.Round(style.TextConfig.SpeakerNameFontSize));

            if (_owner._prototypeManager.TryIndex<FontPrototype>(style.TextConfig.Font, out var fontPrototype))
                label.FontOverride = _owner._resCache.GetFont(fontPrototype.Path, fontSize);

            return label;
        }

        private Control CreatePreviewSpritePlaceholder(AnnouncementStyle style, Vector2 screenSize)
        {
            var clipContainer = new Control
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Top,
                RectClipContent = true
            };

            var clipSize = AnnouncementWidget.GetPortraitClipSize(style);
            clipContainer.SetWidth = clipSize.X;
            clipContainer.SetHeight = clipSize.Y;
            clipContainer.MinWidth = clipSize.X;
            clipContainer.MinHeight = clipSize.Y;

            var placeholder = new PanelContainer
            {
                HorizontalAlignment = HAlignment.Stretch,
                VerticalAlignment = VAlignment.Stretch,
                HorizontalExpand = true,
                VerticalExpand = true
            };

            placeholder.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = style.SpriteConfig.SpriteBoxBorderColor.WithAlpha(0.15f),
                BorderColor = style.SpriteConfig.SpriteBoxBorderColor.WithAlpha(0.85f),
                BorderThickness = new Thickness(Math.Max(1f, style.SpriteConfig.SpriteBoxBorderThickness * 0.75f))
            };

            clipContainer.AddChild(placeholder);

            return WrapPortraitSpriteBox(clipContainer, style);
        }

        private Control WrapPortraitSpriteBox(Control inner, AnnouncementStyle style)
        {
            var cardOpacity = Math.Clamp(style.SpriteConfig.SpriteCardOpacity, 0f, 1f);
            if (!style.SpriteConfig.ShowSpriteBox)
            {
                AnnouncementWidget.ApplyFixedPortraitOuterSize(inner);
                inner.Modulate = Color.White.WithAlpha(cardOpacity);
                return inner;
            }

            var outerPanel = new Control
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Top
            };
            AnnouncementWidget.ApplyFixedPortraitOuterSize(outerPanel);

            var panel = new PanelContainer
            {
                HorizontalAlignment = HAlignment.Stretch,
                VerticalAlignment = VAlignment.Stretch,
                HorizontalExpand = true,
                VerticalExpand = true
            };

            var styleBox = new StyleBoxFlat
            {
                BackgroundColor = style.SpriteConfig.SpriteBoxColor,
                BorderColor = style.SpriteConfig.SpriteBoxBorderColor,
                BorderThickness = new Thickness(style.SpriteConfig.SpriteBoxBorderThickness)
            };

            panel.PanelOverride = styleBox;
            panel.AddChild(inner);
            outerPanel.AddChild(panel);
            _owner.AddSpriteBoxShaderOverlay(style, outerPanel, underlay: true);
            outerPanel.Modulate = Color.White.WithAlpha(cardOpacity);
            return outerPanel;
        }

        private Control WrapPortraitPresentation(Control container, AnnouncementStyle style, Vector2 screenSize)
        {
            return WrapWithCrtIfEnabled(WrapPortraitSpriteBox(container, style), style, screenSize);
        }

        private Control WrapWithCrtIfEnabled(Control container, AnnouncementStyle style, Vector2 screenSize)
        {
            if (!style.AnimationConfig.EnableCRT)
                return container;

            container.Measure(screenSize);
            var width = MathF.Max(1f, container.DesiredSize.X);
            var height = MathF.Max(1f, container.DesiredSize.Y);

            var crtWrapper = new Control
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Top,
                HorizontalExpand = false,
                VerticalExpand = false,
                MinWidth = width,
                MinHeight = height,
                SetWidth = width,
                SetHeight = height
            };

            container.HorizontalAlignment = HAlignment.Stretch;
            container.VerticalAlignment = VAlignment.Stretch;
            container.HorizontalExpand = true;
            container.VerticalExpand = true;
            crtWrapper.AddChild(container);

            var crtOverlay = new CRTOverlay
            {
                Settings = _owner.GetCRTSettingsFromStyle(style),
                HorizontalAlignment = HAlignment.Stretch,
                VerticalAlignment = VAlignment.Stretch,
                HorizontalExpand = true,
                VerticalExpand = true
            };

            crtWrapper.AddChild(crtOverlay);
            return crtWrapper;
        }
    }
}

