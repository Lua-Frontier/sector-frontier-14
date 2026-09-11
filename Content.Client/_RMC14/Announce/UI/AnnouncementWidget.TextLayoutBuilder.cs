using System.Numerics;
using Content.Client.Resources;
using Content.Client._RMC14.Announce.Styling;
using Content.Shared._RMC14.Announce;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Announce;

public sealed partial class AnnouncementWidget
{
    private sealed class TextLayoutBuilder
    {
        private readonly AnnouncementWidget _owner;

        public TextLayoutBuilder(AnnouncementWidget owner)
        {
            _owner = owner;
        }

        public TextLayoutBuildResult BuildTextLayout(
            string[] text,
            string? titleText,
            AnnouncementStyle style,
            bool hasSidePortrait,
            bool hasTitle,
            int titleOffset,
            Vector2 textOffset,
            Vector2 screenSize,
            float portraitColumnWidth,
            float spriteSeparation)
        {
            var screenScaleFactor = AnnouncementStyling.CalculateScreenScaleFactor(screenSize);
            var effectiveTextWidth = AnnouncementStyling.ResolveFixedTextWidth(
                screenSize,
                hasSidePortrait,
                portraitColumnWidth,
                spriteSeparation);
            var labelWrapWidth = AnnouncementStyling.ResolveLabelWrapWidth(effectiveTextWidth);

            var bodyResponsiveFontSize = AnnouncementStyling.CalculateResponsiveFontSize(
                text.Length > 0 ? text : new[] { string.Empty },
                style.TextConfig.FontSize,
                effectiveTextWidth,
                screenSize,
                style);

            var totalLabels = text.Length + titleOffset;
            var labels = new Control[totalLabels];

            var scaleFactor = screenScaleFactor;
            var bodyLineHeight = MathF.Max(1f, style.TextConfig.LineHeight);
            var labelIndex = 0;
            Control? titleLabelRef = null;
            Control? titleTrackRef = null;
            var titleViewportWidth = 0f;
            var titleContentWidth = 0f;
            Control? titleUnderlineRef = null;

            var textAlign = AnnouncementWidget.GetTextAlignment(style, hasSidePortrait);
            var titleAlign = AnnouncementWidget.GetTextAlignment(style, hasSidePortrait);
            var outerContainer = new Control
            {
                HorizontalAlignment = textAlign,
                VerticalAlignment = VAlignment.Top,
                HorizontalExpand = false,
                SetWidth = effectiveTextWidth,
                MinWidth = effectiveTextWidth,
                Margin = new Thickness(textOffset.X * scaleFactor, textOffset.Y * scaleFactor, 0, 0)
            };

            var container = new PanelContainer
            {
                HorizontalAlignment = HAlignment.Stretch,
                VerticalAlignment = VAlignment.Stretch,
                HorizontalExpand = false
            };
            container.SetWidth = effectiveTextWidth;
            container.MinWidth = effectiveTextWidth;

            var textContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalAlignment = textAlign,
                VerticalAlignment = VAlignment.Top,
                HorizontalExpand = false,
                RectClipContent = true
            };
            textContainer.MaxWidth = effectiveTextWidth;
            textContainer.MinWidth = effectiveTextWidth;
            textContainer.SetWidth = effectiveTextWidth;

            if (hasTitle && !string.IsNullOrEmpty(titleText))
            {
                var titleMeasureSize = new Vector2(MathF.Max(screenSize.X * 2f, effectiveTextWidth * 2f), float.PositiveInfinity);
                var titleFontSize = CalculateTitleFontSize(style, screenSize, effectiveTextWidth, titleText);
                var renderLabel = CreateStandaloneTitleLabel(titleText, style, titleFontSize, titleAlign);
                renderLabel.Measure(titleMeasureSize);
                titleContentWidth = renderLabel.DesiredSize.X;

                var titleHeight = MathF.Max(renderLabel.DesiredSize.Y, 1f);
                var titlePadding = CalculateTitleVerticalPadding(style, scaleFactor);
                var titleViewportHeight = titleHeight + titlePadding;
                var titleOffsetY = titlePadding * 0.5f;
                var staticTitleViewportWidth = MathF.Max(effectiveTextWidth, titleContentWidth);
                var titleViewport = new LayoutContainer
                {
                    InheritChildMeasure = false,
                    HorizontalAlignment = titleAlign,
                    VerticalAlignment = VAlignment.Center,
                    HorizontalExpand = false,
                    MinWidth = staticTitleViewportWidth,
                    SetWidth = staticTitleViewportWidth,
                    MinHeight = titleViewportHeight,
                    SetHeight = titleViewportHeight
                };

                LayoutContainer.SetAnchorPreset(renderLabel, LayoutContainer.LayoutPreset.TopLeft);
                LayoutContainer.SetGrowHorizontal(renderLabel, LayoutContainer.GrowDirection.Constrain);
                LayoutContainer.SetGrowVertical(renderLabel, LayoutContainer.GrowDirection.Constrain);

                renderLabel.MinWidth = titleContentWidth;
                renderLabel.SetWidth = titleContentWidth;
                renderLabel.MinHeight = titleHeight;
                renderLabel.SetHeight = titleHeight;
                SetLayoutRect(renderLabel, new Vector2(0f, titleOffsetY), new Vector2(titleContentWidth, titleHeight));
                titleViewport.AddChild(renderLabel);
                titleTrackRef = titleViewport;
                titleViewportWidth = staticTitleViewportWidth;

                if (style.TitleConfig.TitleUnderline)
                {
                    var underlineThickness = Math.Max(1f, style.TitleConfig.TitleUnderlineThickness * scaleFactor);
                    var underlineWidth = MathF.Min(effectiveTextWidth, titleContentWidth);
                    var titleStack = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Vertical,
                        HorizontalAlignment = titleAlign,
                        VerticalAlignment = VAlignment.Top,
                        SeparationOverride = Math.Max(2, (int) MathF.Ceiling(underlineThickness))
                    };

                    var underline = new PanelContainer
                    {
                        HorizontalAlignment = titleAlign,
                        VerticalAlignment = VAlignment.Top,
                        HorizontalExpand = false,
                        VerticalExpand = false,
                        MinWidth = underlineWidth,
                        SetWidth = underlineWidth,
                        MinHeight = underlineThickness,
                        SetHeight = underlineThickness
                    };
                    underline.PanelOverride = new StyleBoxFlat { BackgroundColor = style.TitleConfig.TitleColor };

                    titleStack.AddChild(titleViewport);
                    titleStack.AddChild(underline);
                    textContainer.AddChild(titleStack);

                    var spacerHeight = Math.Max(underlineThickness * 1.5f, bodyResponsiveFontSize * 0.35f);
                    var titleSpacer = new Control
                    {
                        MinHeight = spacerHeight,
                        SetHeight = spacerHeight
                    };
                    textContainer.AddChild(titleSpacer);
                    titleUnderlineRef = underline;
                }
                else
                {
                    textContainer.AddChild(titleViewport);
                }

                var titlePlaceholder = new RichTextLabel();
                labels[labelIndex] = titlePlaceholder;
                titleLabelRef = titlePlaceholder;
                labelIndex++;
            }

            for (var i = 0; i < text.Length; i++)
            {
                var label = new RichTextLabel
                {
                    HorizontalAlignment = textAlign,
                    VerticalAlignment = VAlignment.Top,
                    MaxWidth = labelWrapWidth,
                    HorizontalExpand = false,
                    MinHeight = bodyLineHeight
                };

                textContainer.AddChild(label);
                labels[labelIndex] = label;
                labelIndex++;
            }

            container.AddChild(textContainer);
            outerContainer.AddChild(container);

            outerContainer.Measure(screenSize);
            container.Measure(screenSize);
            textContainer.Measure(screenSize);
            titleLabelRef?.Measure(screenSize);
            titleUnderlineRef?.Measure(screenSize);

            return new TextLayoutBuildResult(
                labels,
                outerContainer,
                effectiveTextWidth,
                Array.Empty<Control>(),
                titleTrackRef,
                titleViewportWidth,
                titleContentWidth,
                0f,
                CalculateTitleFontSize(style, screenSize, effectiveTextWidth, titleText ?? string.Empty));
        }

        public TitleLayoutBuildResult BuildStandaloneTitleLayout(
            string titleText,
            AnnouncementStyle style,
            Vector2 screenSize,
            float titleWidth,
            HAlignment alignment)
        {
            var scaleFactor = AnnouncementStyling.CalculateScreenScaleFactor(screenSize);
            var titleViewportWidth = titleWidth;
            var titleMessage = _owner.CreateFormattedTitleMessage(titleText, style, screenSize, titleWidth);
            var primaryLabel = CreateTitleLabel(alignment, float.PositiveInfinity);
            primaryLabel.SetMessage(titleMessage);

            var titleMeasureSize = new Vector2(MathF.Max(screenSize.X * 2f, titleWidth * 2f), float.PositiveInfinity);
            primaryLabel.Measure(titleMeasureSize);
            var titleFontSize = CalculateTitleFontSize(style, screenSize, titleWidth, titleText);
            var renderLabel = CreateStandaloneTitleLabel(titleText, style, titleFontSize, alignment);
            renderLabel.Measure(titleMeasureSize);
            var titleContentWidth = renderLabel.DesiredSize.X;
            var titleHeight = MathF.Max(renderLabel.DesiredSize.Y, 1f);
            var titlePadding = CalculateTitleVerticalPadding(style, scaleFactor);
            var titleViewportHeight = titleHeight + titlePadding;
            var titleOffsetY = titlePadding * 0.5f;

            var titleTrack = new LayoutContainer
            {
                InheritChildMeasure = false,
                HorizontalAlignment = alignment,
                VerticalAlignment = VAlignment.Center,
                HorizontalExpand = false,
                MinWidth = titleWidth,
                SetWidth = titleWidth,
                MinHeight = titleViewportHeight,
                SetHeight = titleViewportHeight
            };

            LayoutContainer.SetAnchorPreset(renderLabel, LayoutContainer.LayoutPreset.TopLeft);
            LayoutContainer.SetGrowHorizontal(renderLabel, LayoutContainer.GrowDirection.Constrain);
            LayoutContainer.SetGrowVertical(renderLabel, LayoutContainer.GrowDirection.Constrain);

            renderLabel.MinWidth = titleContentWidth;
            renderLabel.SetWidth = titleContentWidth;
            renderLabel.MinHeight = titleHeight;
            renderLabel.SetHeight = titleHeight;
            SetLayoutRect(renderLabel, new Vector2(0f, titleOffsetY), new Vector2(titleContentWidth, titleHeight));
            titleTrack.AddChild(renderLabel);

            Control container = titleTrack;
            if (style.TitleConfig.TitleUnderline)
            {
                var underlineThickness = Math.Max(1f, style.TitleConfig.TitleUnderlineThickness * scaleFactor);
                var underlineWidth = MathF.Min(titleWidth, titleContentWidth);
                var titleStack = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    HorizontalAlignment = alignment,
                    VerticalAlignment = VAlignment.Top,
                    SeparationOverride = Math.Max(2, (int) MathF.Ceiling(underlineThickness))
                };

                var underline = new PanelContainer
                {
                    HorizontalAlignment = alignment,
                    VerticalAlignment = VAlignment.Top,
                    HorizontalExpand = false,
                    VerticalExpand = false,
                    MinWidth = underlineWidth,
                    SetWidth = underlineWidth,
                    MinHeight = underlineThickness,
                    SetHeight = underlineThickness
                };
                underline.PanelOverride = new StyleBoxFlat { BackgroundColor = style.TitleConfig.TitleColor };

                titleStack.AddChild(titleTrack);
                titleStack.AddChild(underline);
                container = titleStack;
            }

            container.Measure(screenSize);

            return new TitleLayoutBuildResult(
                container,
                primaryLabel,
                Array.Empty<Control>(),
                titleTrack,
                titleViewportWidth,
                titleContentWidth,
                0f,
                CalculateTitleFontSize(style, screenSize, titleWidth, titleText));
        }

        private static RichTextLabel CreateTitleLabel(HAlignment alignment, float maxWidth)
        {
            return new RichTextLabel
            {
                HorizontalAlignment = alignment,
                VerticalAlignment = VAlignment.Top,
                MaxWidth = maxWidth,
                HorizontalExpand = false
            };
        }

        private static void SetLayoutRect(Control control, Vector2 position, Vector2 size)
        {
            LayoutContainer.SetMarginLeft(control, position.X);
            LayoutContainer.SetMarginTop(control, position.Y);
            LayoutContainer.SetMarginRight(control, position.X + size.X);
            LayoutContainer.SetMarginBottom(control, position.Y + size.Y);
        }

        private static float CalculateTitleVerticalPadding(AnnouncementStyle style, float scaleFactor)
        {
            return Math.Max(5f * scaleFactor, style.TitleConfig.TitleFontSize * 0.25f);
        }

        private Label CreateStandaloneTitleLabel(
            string titleText,
            AnnouncementStyle style,
            float titleFontSize,
            HAlignment alignment)
        {
            var label = new Label
            {
                Text = titleText,
                ClipText = false,
                FontColorOverride = style.TitleConfig.TitleColor,
                HorizontalAlignment = alignment,
                VerticalAlignment = VAlignment.Center,
                Align = alignment switch
                {
                    HAlignment.Left => Label.AlignMode.Left,
                    HAlignment.Right => Label.AlignMode.Right,
                    _ => Label.AlignMode.Center
                },
                VAlign = Label.VAlignMode.Center
            };

            if (_owner._prototypeManager.TryIndex<FontPrototype>(style.TitleConfig.TitleFont, out var fontPrototype))
            {
                label.FontOverride = _owner._resCache.GetFont(fontPrototype.Path, Math.Max(1, (int)MathF.Round(titleFontSize)));
            }

            return label;
        }

        private static float CalculateTitleFontSize(AnnouncementStyle style, Vector2 screenSize, float maxAllowedWidth, string titleText)
        {
            return AnnouncementStyling.CalculateResponsiveFontSize(
                new[] { titleText },
                style.TitleConfig.TitleFontSize,
                maxAllowedWidth,
                screenSize,
                style);
        }
    }

    private readonly record struct TextLayoutBuildResult(
        Control[] Labels,
        Control Container,
        float MaxAllowedWidth,
        Control[] TitleLabels,
        Control? TitleTrack,
        float TitleViewportWidth,
        float TitleContentWidth,
        float TitleScrollGap,
        float TitleRenderedFontSize);

    private readonly record struct TitleLayoutBuildResult(
        Control Container,
        Control PrimaryLabel,
        Control[] TitleLabels,
        Control TitleTrack,
        float TitleViewportWidth,
        float TitleContentWidth,
        float TitleScrollGap,
        float TitleRenderedFontSize);
}
