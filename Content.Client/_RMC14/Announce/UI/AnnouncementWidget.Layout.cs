using System.Numerics;
using Content.Client._RMC14.Announce.Styling;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Inventory.Widgets;
using Content.Shared._RMC14.Announce;
using Content.Shared._RMC14.Announce.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Linq;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.Log;

namespace Content.Client._RMC14.Announce;

public sealed partial class AnnouncementWidget
{
    private const float HudSeparation = 16f;
    internal const float PortraitOuterWidth = 150f;
    internal const float PortraitOuterHeight = 100f;
    internal const float PortraitSpriteTopOffset = 0f;
    private const float PortraitSpriteRenderScale = 3.2f;
    private const float PortraitSpriteSizeMultiplier = 2.5f;
    private const float PortraitFlagRenderScale = 2.0f;
    private const float PortraitFlagSizeMultiplier = 2.3f;

    private void SetupUI()
    {
        if (ActiveAnnouncement == null)
            return;

        RemoveAllChildren();
        _textContainers.Clear();

        var announcement = ActiveAnnouncement.Data;
        var style = ActiveAnnouncement.ResolvedStyle;
        var screenSize = ResolveScreenSize();
        var screenScaleFactor = AnnouncementStyling.CalculateScreenScaleFactor(screenSize);
        var spriteSeparation = CalculateSpriteSeparation(style, screenScaleFactor);

        var titleText = announcement.Title;
        _hasTitle = style.TitleConfig.ShowTitle && !string.IsNullOrEmpty(titleText);
        _titleOffset = _hasTitle ? 1 : 0;

        _spriteContainer = _spriteBuilder.CreateSpriteContainer(announcement, style, screenSize);
        var hasSidePortrait = announcement.IsFactionFlagPortrait ||
            (_spriteContainer != null &&
             (style.LayoutConfig.SpritePosition == AnnouncementSpritePosition.Left ||
              style.LayoutConfig.SpritePosition == AnnouncementSpritePosition.Right));
        var contentAlignment = GetTextAlignment(style, hasSidePortrait);

        var contentContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = contentAlignment,
            VerticalAlignment = VAlignment.Top,
            SeparationOverride = spriteSeparation
        };

        var titleSpansAnnouncement = _hasTitle &&
            _spriteContainer != null &&
            style.LayoutConfig.SpritePosition is AnnouncementSpritePosition.Left or AnnouncementSpritePosition.Right &&
            style.LayoutConfig.TitlePosition is AnnouncementTitlePosition.Above or AnnouncementTitlePosition.Below;

        var fixedWindowWidth = AnnouncementStyling.ResolveFixedWindowWidth(screenSize);
        var portraitColumnWidth = hasSidePortrait ? EstimatePortraitColumnWidth(style, screenScaleFactor) : 0f;

        var textLayout = _textLayoutBuilder.BuildTextLayout(
            announcement.Text,
            titleSpansAnnouncement ? null : titleText,
            style,
            hasSidePortrait,
            _hasTitle && !titleSpansAnnouncement,
            titleSpansAnnouncement ? 0 : _titleOffset,
            Vector2.Zero,
            screenSize,
            portraitColumnWidth,
            spriteSeparation);

        var resolvedTextWidth = textLayout.MaxAllowedWidth;
        _textContainers.Add(textLayout.Container);
        ApplyTextStyling();
        _activeTextMaxWidth = resolvedTextWidth;

        if (_spriteContainer != null)
        {
            var spritePos = style.LayoutConfig.SpritePosition;
            var spriteWrapper = new Control
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Top,
                HorizontalExpand = false,
                VerticalExpand = false
            };
            spriteWrapper.AddChild(_spriteContainer);

            if (spritePos == AnnouncementSpritePosition.Left || spritePos == AnnouncementSpritePosition.Above)
            {
                if (spritePos == AnnouncementSpritePosition.Above)
                {
                    var spriteVerticalContainer = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Vertical,
                        HorizontalAlignment = HAlignment.Center,
                        VerticalAlignment = VAlignment.Top,
                        SeparationOverride = spriteSeparation,
                        HorizontalExpand = true,
                        VerticalExpand = true
                    };
                    spriteVerticalContainer.AddChild(spriteWrapper);
                    foreach (var container in _textContainers)
                    {
                        spriteVerticalContainer.AddChild(container);
                    }
                    contentContainer.AddChild(spriteVerticalContainer);
                }
                else
                {
                    contentContainer.AddChild(spriteWrapper);
                    foreach (var container in _textContainers)
                    {
                        contentContainer.AddChild(container);
                    }
                }
            }
            else if (spritePos == AnnouncementSpritePosition.Below)
            {
                var spriteVerticalContainer = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Top,
                    SeparationOverride = spriteSeparation,
                    HorizontalExpand = true,
                    VerticalExpand = true
                };
                foreach (var container in _textContainers)
                {
                    spriteVerticalContainer.AddChild(container);
                }
                spriteVerticalContainer.AddChild(spriteWrapper);
                contentContainer.AddChild(spriteVerticalContainer);
            }
            else
            {
                foreach (var container in _textContainers)
                {
                    contentContainer.AddChild(container);
                }
                contentContainer.AddChild(spriteWrapper);
            }
        }
        else
        {
            foreach (var container in _textContainers)
            {
                contentContainer.AddChild(container);
            }
        }

        if (hasSidePortrait &&
            style.LayoutConfig.SpritePosition is AnnouncementSpritePosition.Left or AnnouncementSpritePosition.Right)
        {
            contentContainer.MinWidth = fixedWindowWidth;
            contentContainer.SetWidth = fixedWindowWidth;
        }

        if (titleSpansAnnouncement)
        {
            var standaloneTitleWidth = fixedWindowWidth;
            var titleAlignment = GetTextAlignment(style, _spriteContainer != null);
            var titleBuild = _textLayoutBuilder.BuildStandaloneTitleLayout(
                titleText,
                style,
                screenSize,
                standaloneTitleWidth,
                titleAlignment);

            _richTextLabels = new Control[] { titleBuild.PrimaryLabel }.Concat(textLayout.Labels).ToArray();
            ActiveAnnouncement.TitleLabels = titleBuild.TitleLabels;
            ActiveAnnouncement.TitleTrack = titleBuild.TitleTrack;
            ActiveAnnouncement.TitleViewportWidth = titleBuild.TitleViewportWidth;
            ActiveAnnouncement.TitleContentWidth = titleBuild.TitleContentWidth;
            ActiveAnnouncement.TitleScrollGap = titleBuild.TitleScrollGap;
            ActiveAnnouncement.TitleText = titleText;
            ActiveAnnouncement.TitleRenderedFontSize = titleBuild.TitleRenderedFontSize;

            var rootSeparation = Math.Max(2, (int)MathF.Ceiling(style.TextConfig.LineHeight * 0.15f));
            var root = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalAlignment = contentAlignment,
                VerticalAlignment = VAlignment.Top,
                SeparationOverride = rootSeparation
            };

            if (style.LayoutConfig.TitlePosition == AnnouncementTitlePosition.Above)
            {
                root.AddChild(titleBuild.Container);
                root.AddChild(contentContainer);
            }
            else
            {
                root.AddChild(contentContainer);
                root.AddChild(titleBuild.Container);
            }

            AddChild(root);
        }
        else
        {
            _richTextLabels = textLayout.Labels;
            ActiveAnnouncement.TitleLabels = textLayout.TitleLabels;
            ActiveAnnouncement.TitleTrack = textLayout.TitleTrack;
            ActiveAnnouncement.TitleViewportWidth = textLayout.TitleViewportWidth;
            ActiveAnnouncement.TitleContentWidth = textLayout.TitleContentWidth;
            ActiveAnnouncement.TitleScrollGap = textLayout.TitleScrollGap;
            ActiveAnnouncement.TitleText = titleText;
            ActiveAnnouncement.TitleRenderedFontSize = textLayout.TitleRenderedFontSize;
            AddChild(contentContainer);
        }

        SetInitialVisibility();
        FinalizeLayoutSize(screenSize, fixedWindowWidth);
    }

    private void FinalizeLayoutSize(Vector2 screenSize, float fixedWindowWidth)
    {
        SetAllLabelsText();
        Measure(screenSize);

        var width = MathF.Max(DesiredSize.X, fixedWindowWidth);
        var height = MathF.Max(DesiredSize.Y, 0f);
        _cachedLayoutSize = new Vector2(width, height);
        if (width <= 0f || height <= 0f)
            return;

        MinWidth = width;
        MinHeight = height;
        SetWidth = width;
        SetHeight = height;
    }

    private static int CalculateSpriteSeparation(AnnouncementStyle style, float screenScaleFactor)
    {
        var configured = Math.Max(0f, style.LayoutConfig.SpriteSpacing);
        var fontDriven = Math.Max(
            style.TextConfig.FontSize * 0.20f,
            style.TextConfig.ShowSpeakerName ? style.TextConfig.SpeakerNameFontSize * 0.50f : 0f);

        if (style.SpriteConfig.ShowSpriteBox)
            fontDriven = Math.Max(fontDriven, style.SpriteConfig.SpriteBoxBorderThickness * 2f);

        var spacing = MathF.Max(configured, MathF.Max(2f * screenScaleFactor, fontDriven));
        return Math.Max(0, (int)MathF.Ceiling(spacing));
    }

    private static int CalculateSpeakerNameSeparation(AnnouncementStyle style, float screenScaleFactor)
    {
        var configured = Math.Max(0f, style.LayoutConfig.SpriteSpacing * 0.5f);
        var fontDriven = Math.Max(
            style.TextConfig.SpeakerNameFontSize * 0.35f,
            style.TextConfig.FontSize * 0.15f);

        var spacing = MathF.Max(configured, MathF.Max(2f * screenScaleFactor, fontDriven));
        return Math.Max(0, (int)MathF.Ceiling(spacing));
    }

    internal static float EstimatePortraitColumnWidth(AnnouncementStyle style, float screenScaleFactor)
    {
        var width = PortraitOuterWidth;
        if (style.TextConfig.ShowSpeakerName)
            width = MathF.Max(width, style.TextConfig.SpeakerNameFontSize * 14f);
        return width;
    }

    internal static float GetPortraitBorderInset(AnnouncementStyle style) =>
        style.SpriteConfig.ShowSpriteBox ? style.SpriteConfig.SpriteBoxBorderThickness : 0f;

    internal static Vector2 GetPortraitClipSize(AnnouncementStyle style)
    {
        var inset = GetPortraitBorderInset(style) * 2f;
        return new Vector2(PortraitOuterWidth - inset, PortraitOuterHeight - inset);
    }

    internal static void ApplyPortraitTuning(Control portrait, AnnouncementStyle style)
    {
        var offset = style.SpriteConfig.SpriteOffset;
        portrait.Margin = new Thickness(offset.X, offset.Y, -offset.X, -offset.Y);
    }

    internal static void ApplyFixedPortraitOuterSize(Control outerPanel)
    {
        outerPanel.SetWidth = PortraitOuterWidth;
        outerPanel.SetHeight = PortraitOuterHeight;
        outerPanel.MinWidth = PortraitOuterWidth;
        outerPanel.MinHeight = PortraitOuterHeight;
    }

    internal static void ApplyFixedPortraitSpriteLayout(Control clipContainer, SpriteView spriteView, AnnouncementStyle style)
    {
        var clipSize = GetPortraitClipSize(style);
        clipContainer.SetWidth = clipSize.X;
        clipContainer.SetHeight = clipSize.Y;
        clipContainer.MinWidth = clipSize.X;
        clipContainer.MinHeight = clipSize.Y;
        spriteView.Scale = new Vector2(PortraitSpriteRenderScale, PortraitSpriteRenderScale);
        spriteView.SetWidth = clipSize.X * PortraitSpriteSizeMultiplier;
        spriteView.SetHeight = clipSize.Y * PortraitSpriteSizeMultiplier;
        spriteView.Margin = new Thickness(0, -PortraitSpriteTopOffset, 0, 0);
        spriteView.VerticalAlignment = VAlignment.Top;
        spriteView.HorizontalAlignment = HAlignment.Center;
        spriteView.Stretch = SpriteView.StretchMode.Fill;
    }

    internal static void ApplyFixedPortraitFlagLayout(Control clipContainer, SpriteView spriteView, AnnouncementStyle style)
    {
        var clipSize = GetPortraitClipSize(style);
        clipContainer.SetWidth = clipSize.X;
        clipContainer.SetHeight = clipSize.Y;
        clipContainer.MinWidth = clipSize.X;
        clipContainer.MinHeight = clipSize.Y;
        var scale = PortraitFlagRenderScale * style.SpriteConfig.SpriteScale;
        spriteView.Scale = new Vector2(scale, scale);
        spriteView.SetWidth = clipSize.X * PortraitFlagSizeMultiplier;
        spriteView.SetHeight = clipSize.Y * PortraitFlagSizeMultiplier;
        spriteView.Margin = new Thickness();
        spriteView.VerticalAlignment = VAlignment.Center;
        spriteView.HorizontalAlignment = HAlignment.Center;
        spriteView.Stretch = SpriteView.StretchMode.Fill;
    }

    private void SetSpriteDisplayProperties(Control clipContainer, SpriteView spriteView, AnnouncementStyle style, float spriteScale, float screenScaleFactor)
    {
        if (style.LayoutConfig.SpriteDisplayMode == SpriteDisplayMode.TopHalf)
        {
            ApplyFixedPortraitSpriteLayout(clipContainer, spriteView, style);
            return;
        }

        var baseContainerWidth = 120f * screenScaleFactor;
        var baseContainerHeight = 120f * screenScaleFactor;
        const float spriteMultiplier = 2.0f;

        clipContainer.SetWidth = baseContainerWidth * spriteScale;
        clipContainer.SetHeight = baseContainerHeight * spriteScale * 2f;
        spriteView.SetWidth = baseContainerWidth * spriteScale * spriteMultiplier;
        spriteView.SetHeight = baseContainerHeight * spriteScale * spriteMultiplier;
        spriteView.VerticalAlignment = VAlignment.Center;

        spriteView.HorizontalAlignment = HAlignment.Center;
        spriteView.Stretch = SpriteView.StretchMode.Fill;
    }

    private static HAlignment GetTextAlignment(AnnouncementStyle style, bool hasSpriteContent)
    {
        if (hasSpriteContent)
        {
            if (style.LayoutConfig.SpritePosition == AnnouncementSpritePosition.Left)
                return HAlignment.Left;

            if (style.LayoutConfig.SpritePosition == AnnouncementSpritePosition.Right)
                return HAlignment.Right;
        }

        return style.LayoutConfig.Position switch
        {
            AnnouncementPosition.TopLeft or AnnouncementPosition.MiddleLeft or AnnouncementPosition.BottomLeft => HAlignment.Left,
            AnnouncementPosition.TopRight or AnnouncementPosition.MiddleRight or AnnouncementPosition.BottomRight => HAlignment.Right,
            _ => HAlignment.Center
        };
    }

    private void AddSpriteBoxShaderOverlay(AnnouncementStyle style, Control container, bool underlay)
    {
        if (string.IsNullOrWhiteSpace(style.SpriteConfig.SpriteBoxShader))
            return;

        if (!_prototypeManager.TryIndex<ShaderPrototype>(style.SpriteConfig.SpriteBoxShader, out var shaderPrototype))
        {
            Logger.Warning($"[AnnouncementWidget] Sprite box shader '{style.SpriteConfig.SpriteBoxShader}' not found.");
            return;
        }

        var overlay = new TextureRect
        {
            Texture = Texture.White,
            Stretch = TextureRect.StretchMode.Scale,
            ShaderOverride = shaderPrototype.Instance(),
            MouseFilter = Control.MouseFilterMode.Ignore,
            HorizontalAlignment = HAlignment.Stretch,
            VerticalAlignment = VAlignment.Stretch,
            HorizontalExpand = true,
            VerticalExpand = true
        };

        container.AddChild(overlay);
        if (underlay)
            overlay.SetPositionFirst();
    }

    private CRTSettings GetCRTSettingsFromStyle(AnnouncementStyle style)
    {
        if (style.AnimationConfig.EnableCRT &&
            style.AnimationConfig.CRTSettings != null)
        {
            return style.AnimationConfig.CRTSettings;
        }

        return new CRTSettings
        {
            Enabled = true,
            ShowScanlines = true,
            ScanlineSpacing = 3f,
            ScanlineAlpha = 0.8f,
            ScanlineThickness = 2f,
            NoiseIntensity = 0.5f,
            GlowColor = Color.FromHex("#ffffff"),
            VignetteIntensity = 0.3f,
            ShowNoise = true,
            ShowVignette = true
        };
    }

    private void ApplyTextStyling()
    {
        if (ActiveAnnouncement == null)
            return;

        var style = ActiveAnnouncement.ResolvedStyle;

        foreach (var outerContainer in _textContainers)
        {
            if (outerContainer.Children.FirstOrDefault() is PanelContainer panel)
            {
                var styleBox = new StyleBoxFlat();

                if (style.BackgroundConfig.ShowBackground)
                {
                    styleBox.BackgroundColor = style.BackgroundConfig.BackgroundColor.WithAlpha(style.BackgroundConfig.BackgroundAlpha);
                    const float padding = 10f;
                    styleBox.ContentMarginTopOverride = padding;
                    styleBox.ContentMarginBottomOverride = padding;
                    styleBox.ContentMarginLeftOverride = padding;
                    styleBox.ContentMarginRightOverride = padding;
                }
                else
                {
                    styleBox.BackgroundColor = Color.Transparent;
                }

                styleBox.BorderThickness = new Thickness(0);
                panel.PanelOverride = styleBox;
            }
        }
    }

    private void SetInitialVisibility()
    {
        if (ActiveAnnouncement == null)
            return;

        var animation = ActiveAnnouncement.ResolvedStyle.AnimationConfig.Animation;

        if (animation is TypewriterAnimationConfig or GlitchAnimationConfig)
        {
            for (var i = _titleOffset; i < _richTextLabels.Length; i++)
            {
                (_richTextLabels[i] as RichTextLabel)?.SetMessage(FormattedMessage.FromMarkupPermissive(""));
            }
        }
        else
        {
            SetAllLabelsText();
        }
    }

    private FormattedMessage CreateFormattedMessage(string text, AnnouncementStyle style)
    {
        return CreateFormattedMessageWithOverrides(text, style, null, null, null);
    }

    private FormattedMessage CreateFormattedMessageWithOverrides(
        string text,
        AnnouncementStyle style,
        float? fontSizeOverride,
        Color? colorOverride,
        string? fontOverride)
    {
        var screenSize = ResolveScreenSize();
        var screenScaleFactor = AnnouncementStyling.CalculateScreenScaleFactor(screenSize);
        var hasSidePortrait = _spriteContainer != null ||
            (ActiveAnnouncement?.Data.IsFactionFlagPortrait ?? false);
        var portraitColumnWidth = hasSidePortrait ? EstimatePortraitColumnWidth(style, screenScaleFactor) : 0f;
        var spriteSeparation = hasSidePortrait ? CalculateSpriteSeparation(style, screenScaleFactor) : 0;
        var maxAllowedWidth = _activeTextMaxWidth > 0f
            ? AnnouncementStyling.ResolveLabelWrapWidth(_activeTextMaxWidth)
            : AnnouncementStyling.ResolveLabelWrapWidth(
                AnnouncementStyling.ResolveFixedTextWidth(
                    screenSize,
                    hasSidePortrait,
                    portraitColumnWidth,
                    spriteSeparation));
        var baseFontSize = fontSizeOverride ?? style.TextConfig.FontSize;
        var responsiveFontSize = AnnouncementStyling.CalculateResponsiveFontSize(
            ActiveAnnouncement?.Data.Text ?? new[] { text },
            baseFontSize,
            maxAllowedWidth,
            screenSize,
            style);

        return AnnouncementStyling.CreateFormattedMessage(
            text,
            responsiveFontSize,
            colorOverride ?? style.TextConfig.PrimaryColor,
            fontOverride ?? style.TextConfig.Font);
    }

    private FormattedMessage CreateFormattedTitleMessage(string text, AnnouncementStyle style, Vector2 screenSize, float maxAllowedWidth)
    {
        var responsiveFontSize = AnnouncementStyling.CalculateResponsiveFontSize(new[] { text }, style.TitleConfig.TitleFontSize, maxAllowedWidth, screenSize, style);
        return AnnouncementStyling.CreateFormattedMessage(text, responsiveFontSize, style.TitleConfig.TitleColor, style.TitleConfig.TitleFont);
    }

    private void UpdatePosition()
    {
        var layout = ResolveLayout();
        if (layout.Size.X <= 0f || layout.Size.Y <= 0f)
            return;

        ApplyManagedLayout(layout.Position, layout.Size);
    }

    internal AnnouncementWidgetLayout ResolveLayout(Vector2? sizeOverride = null)
    {
        if (Parent is not Control parent || ActiveAnnouncement == null)
            return default;

        var screenSize = parent.Size.X > 0f && parent.Size.Y > 0f
            ? parent.Size
            : ResolveScreenSize();

        // Position within the gameplay viewport
        var viewportOffset = Vector2.Zero;
        var positioningSize = screenSize;
        if (ForcedScreenSize == null)
        {
            var viewport = _uiManager.ActiveScreen?.GetWidget<MainViewport>();
            if (viewport is { Size.X: > 0f, Size.Y: > 0f })
            {
                positioningSize = viewport.Size;
                viewportOffset = viewport.GlobalPosition - parent.GlobalPosition;
            }
        }

        Vector2 widgetSize;
        if (sizeOverride is { } resolvedSize)
        {
            widgetSize = resolvedSize;
        }
        else
        {
            if (_cachedLayoutSize.X > 0f && _cachedLayoutSize.Y > 0f)
                widgetSize = _cachedLayoutSize;
            else
            {
                Measure(positioningSize);
                widgetSize = DesiredSize;
            }
        }

        var announcement = ActiveAnnouncement.Data;
        var style = ActiveAnnouncement.ResolvedStyle;

        var position = CalculatePosition(positioningSize, widgetSize, announcement, style);
        position += viewportOffset;
        position = AvoidHudOverlap(
            position,
            widgetSize,
            parent,
            viewportOffset,
            positioningSize,
            announcement,
            style);

        return new AnnouncementWidgetLayout(position, widgetSize);
    }

    internal HAlignment ResolveStackAlignment()
    {
        if (ActiveAnnouncement == null || ActiveAnnouncement.Data.ScreenPositionOverride != null)
            return HAlignment.Left;

        return ActiveAnnouncement.ResolvedStyle.LayoutConfig.Position switch
        {
            AnnouncementPosition.TopRight or AnnouncementPosition.MiddleRight or AnnouncementPosition.BottomRight => HAlignment.Right,
            AnnouncementPosition.TopCenter or AnnouncementPosition.MiddleCenter or AnnouncementPosition.BottomCenter or AnnouncementPosition.FullScreen => HAlignment.Center,
            _ => HAlignment.Left
        };
    }

    internal void ApplyManagedLayout(Vector2 position, Vector2 size)
    {
        UpdateLayoutRect(position, size);
    }

    private Vector2 AvoidHudOverlap(
        Vector2 position,
        Vector2 widgetSize,
        Control parent,
        Vector2 viewportOffset,
        Vector2 viewportSize,
        AnnouncementDisplayData announcement,
        AnnouncementStyle style)
    {
        if (announcement.ScreenPositionOverride != null ||
            style.LayoutConfig.Position == AnnouncementPosition.FullScreen)
        {
            return position;
        }

        var viewportBounds = UIBox2.FromDimensions(viewportOffset, viewportSize);
        if (style.LayoutConfig.Position == AnnouncementPosition.BottomLeft)
        {
            position = AvoidExpandedInventory(
                position,
                widgetSize,
                parent,
                viewportSize,
                viewportBounds);
        }

        var actions = _uiManager.ActiveScreen?.GetWidget<ActionsBar>()?.ActionsContainer;
        if (actions is not { VisibleInTree: true } || actions.Size.X <= 0f || actions.Size.Y <= 0f)
            return position;

        var actionBarPosition = actions.GlobalPosition - parent.GlobalPosition;
        var actionBarBounds = UIBox2.FromDimensions(actionBarPosition, actions.Size);

        return AvoidOverlap(position, widgetSize, actionBarBounds, viewportBounds, HudSeparation);
    }

    private Vector2 AvoidExpandedInventory(
        Vector2 position,
        Vector2 widgetSize,
        Control parent,
        Vector2 viewportSize,
        UIBox2 viewportBounds)
    {
        var inventory = _uiManager.ActiveScreen?.GetWidget<InventoryGui>();
        if (inventory == null)
            return position;

        // Measure the slot grid directly so the reserved area is identical whether the inventory is open or closed.
        inventory.InventoryHotbar.Measure(viewportSize);
        var expandedSize = inventory.InventoryHotbar.DesiredSize;
        if (expandedSize.X <= 0f || expandedSize.Y <= 0f)
            return position;

        var inventoryPosition = inventory.GlobalPosition - parent.GlobalPosition;
        var inventoryBottom = inventoryPosition.Y + inventory.Size.Y;
        var expandedBounds = UIBox2.FromDimensions(
            new Vector2(inventoryPosition.X, inventoryBottom - expandedSize.Y),
            expandedSize);

        return PlaceOutsideBottomLeftHudArea(
            position,
            widgetSize,
            expandedBounds,
            viewportBounds,
            HudSeparation);
    }

    internal static Vector2 PlaceOutsideBottomLeftHudArea(
        Vector2 position,
        Vector2 size,
        UIBox2 reservedArea,
        UIBox2 bounds,
        float separation)
    {
        var positionRight = new Vector2(
            Math.Max(position.X, reservedArea.Right + separation),
            position.Y);
        if (positionRight.X + size.X <= bounds.Right)
            return positionRight;

        var positionAbove = new Vector2(
            Math.Clamp(position.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - size.X)),
            reservedArea.Top - size.Y - separation);
        if (positionAbove.Y >= bounds.Top)
            return positionAbove;

        // Extremely small viewports cannot fit both rectangles without overlap. Keep the title on-screen.
        return new Vector2(
            Math.Clamp(positionRight.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - size.X)),
            Math.Clamp(position.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - size.Y)));
    }

    internal static Vector2 AvoidOverlap(
        Vector2 position,
        Vector2 size,
        UIBox2 obstacle,
        UIBox2 bounds,
        float separation)
    {
        var announcementBounds = UIBox2.FromDimensions(position, size);
        if (!announcementBounds.Intersects(obstacle))
            return position;

        var positionRight = new Vector2(obstacle.Right + separation, position.Y);
        if (positionRight.X + size.X <= bounds.Right)
            return positionRight;

        var positionBelow = new Vector2(position.X, obstacle.Bottom + separation);
        if (positionBelow.Y + size.Y <= bounds.Bottom)
            return positionBelow;

        return positionRight;
    }

    private static Vector2 CalculatePosition(Vector2 screenSize, Vector2 widgetSize, AnnouncementDisplayData announcement, AnnouncementStyle style)
    {
        if (announcement.ScreenPositionOverride is { } normalizedPosition)
            return CalculateCustomPosition(screenSize, widgetSize, normalizedPosition);

        return CalculateStylePosition(screenSize, widgetSize, style);
    }

    private static Vector2 CalculateStylePosition(Vector2 screenSize, Vector2 widgetSize, AnnouncementStyle style)
    {
        const float padding = 50f;
        const float topPadding = 100f;

        return style.LayoutConfig.Position switch
        {
            AnnouncementPosition.TopLeft => new Vector2(padding, topPadding),
            AnnouncementPosition.TopCenter => new Vector2((screenSize.X - widgetSize.X) / 2, padding),
            AnnouncementPosition.TopRight => new Vector2(screenSize.X - widgetSize.X - padding, topPadding),
            AnnouncementPosition.MiddleLeft => new Vector2(padding, (screenSize.Y - widgetSize.Y) / 2),
            AnnouncementPosition.MiddleCenter => new Vector2((screenSize.X - widgetSize.X) / 2, (screenSize.Y - widgetSize.Y) / 2),
            AnnouncementPosition.MiddleRight => new Vector2(screenSize.X - widgetSize.X - padding, (screenSize.Y - widgetSize.Y) / 2),
            AnnouncementPosition.BottomLeft => new Vector2(padding, screenSize.Y - widgetSize.Y - padding),
            AnnouncementPosition.BottomCenter => new Vector2((screenSize.X - widgetSize.X) / 2, screenSize.Y - widgetSize.Y - padding),
            AnnouncementPosition.BottomRight => new Vector2(screenSize.X - widgetSize.X - padding, screenSize.Y - widgetSize.Y - padding),
            AnnouncementPosition.FullScreen => Vector2.Zero,
            _ => new Vector2((screenSize.X - widgetSize.X) / 2, (screenSize.Y - widgetSize.Y) / 2)
        };
    }

    private static Vector2 CalculateCustomPosition(Vector2 screenSize, Vector2 widgetSize, Vector2 normalizedPosition)
    {
        var clamped = new Vector2(
            Math.Clamp(normalizedPosition.X, 0f, 1f),
            Math.Clamp(normalizedPosition.Y, 0f, 1f));

        var position = new Vector2(screenSize.X * clamped.X, screenSize.Y * clamped.Y);

        const float minVisible = 48f;
        position.X = ClampPositionAxis(position.X, widgetSize.X, screenSize.X, minVisible);
        position.Y = ClampPositionAxis(position.Y, widgetSize.Y, screenSize.Y, minVisible);
        return position;
    }

    private static float ClampPositionAxis(float position, float size, float screenSize, float minVisible)
    {
        var min = minVisible - size;
        var max = screenSize - minVisible;
        if (min > max)
            return (screenSize - size) * 0.5f;

        return Math.Clamp(position, min, max);
    }

    private void UpdateLayoutRect(Vector2 position, Vector2 size)
    {
        LayoutContainer.SetMarginLeft(this, position.X);
        LayoutContainer.SetMarginTop(this, position.Y);
        LayoutContainer.SetMarginRight(this, position.X + size.X);
        LayoutContainer.SetMarginBottom(this, position.Y + size.Y);
    }
}

internal readonly record struct AnnouncementWidgetLayout(Vector2 Position, Vector2 Size);

