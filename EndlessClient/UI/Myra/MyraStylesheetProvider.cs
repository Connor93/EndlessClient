using AutomaticTypeMapper;
using EndlessClient.UI.Styles;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI.Styles;

namespace EndlessClient.UI.Myra
{
    /// <summary>
    /// Builds a Myra Stylesheet from IUIStyleProvider colors and IMyraFontProvider fonts.
    /// Supports hot-swapping: call Rebuild() then Apply() to change themes at runtime.
    ///
    /// All style instances must be created explicitly — Myra's Stylesheet.GetDefaultStyle
    /// throws if no default style has been set for a widget type.
    /// </summary>
    [AutoMappedType(IsSingleton = true)]
    public class MyraStylesheetProvider : IMyraStylesheetProvider
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly IMyraFontProvider _fontProvider;

        private Stylesheet? _cachedStylesheet;

        public MyraStylesheetProvider(IUIStyleProvider styleProvider, IMyraFontProvider fontProvider)
        {
            _styleProvider = styleProvider;
            _fontProvider = fontProvider;
        }

        public Stylesheet Stylesheet => _cachedStylesheet ??= BuildStylesheet();

        public void Rebuild()
        {
            _cachedStylesheet = BuildStylesheet();
        }

        public void Apply()
        {
            Stylesheet.Current = Stylesheet;
        }

        private Stylesheet BuildStylesheet()
        {
            var normalFont = _fontProvider.Normal;
            var headerFont = _fontProvider.Header;

            // Clone the default stylesheet to preserve the Atlas (contains "white" texture region
            // required by SolidBrush.Draw). Then override all styles with our theme.
            var stylesheet = DefaultAssets.DefaultStylesheet.Clone();

            // Desktop background (transparent — game world shows through)
            stylesheet.DesktopStyle = new DesktopStyle
            {
                Background = null
            };

            // Label styles
            stylesheet.LabelStyle = new LabelStyle
            {
                Font = normalFont,
                TextColor = _styleProvider.TextPrimary
            };

            // Tooltip styles
            stylesheet.TooltipStyle = new LabelStyle
            {
                Font = normalFont,
                TextColor = _styleProvider.TooltipText,
                Background = new SolidBrush(_styleProvider.TooltipBackground),
                Border = new SolidBrush(_styleProvider.TooltipBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4)
            };

            // TextBox styles
            stylesheet.TextBoxStyle = new TextBoxStyle
            {
                Font = normalFont,
                TextColor = _styleProvider.InputText,
                Background = new SolidBrush(_styleProvider.InputBackground),
                Border = new SolidBrush(_styleProvider.InputBorder),
                BorderThickness = new Thickness(_styleProvider.BorderThickness),
                Padding = new Thickness(4),
                FocusedBorder = new SolidBrush(_styleProvider.TextHighlight),
                FocusedBackground = new SolidBrush(_styleProvider.InputBackground)
            };

            // Button styles
            stylesheet.ButtonStyle = new ButtonStyle
            {
                Background = new SolidBrush(_styleProvider.ButtonNormal),
                OverBackground = new SolidBrush(_styleProvider.ButtonHover),
                PressedBackground = new SolidBrush(_styleProvider.ButtonPressed),
                Border = new SolidBrush(_styleProvider.ButtonBorder),
                BorderThickness = new Thickness(_styleProvider.BorderThickness),
                Padding = new Thickness(_styleProvider.ButtonPadding, 6),
                DisabledBackground = new SolidBrush(_styleProvider.ButtonDisabled),
                LabelStyle = new LabelStyle
                {
                    Font = normalFont,
                    TextColor = _styleProvider.ButtonText
                }
            };

            // Window styles
            // Note: Myra v1.5.10 WindowStyle only has TitleStyle and CloseButtonStyle.
            // The window background comes from the base WidgetStyle.Background.
            stylesheet.WindowStyle = new WindowStyle
            {
                Background = new SolidBrush(_styleProvider.PanelBackground),
                Border = new SolidBrush(_styleProvider.PanelBorder),
                BorderThickness = new Thickness(_styleProvider.BorderThickness),
                Padding = new Thickness(8),
                TitleStyle = new LabelStyle
                {
                    Font = headerFont,
                    TextColor = _styleProvider.TitleBarText,
                    Background = new SolidBrush(_styleProvider.TitleBarBackground),
                    Padding = new Thickness(6, 4)
                },
                CloseButtonStyle = new ImageButtonStyle()
            };

            // ScrollViewer styles
            // Note: ScrollViewerStyle requires IImage properties for scrollbar visuals.
            // Leaving the default style so Myra's built-in scroll bar images are used.
            // Custom scroll theming can be added later with proper texture regions.

            // ComboBox styles
            stylesheet.ComboBoxStyle = new ComboBoxStyle
            {
                Background = new SolidBrush(_styleProvider.InputBackground),
                LabelStyle = new LabelStyle
                {
                    Font = normalFont,
                    TextColor = _styleProvider.InputText
                },
                ListBoxStyle = new ListBoxStyle
                {
                    Background = new SolidBrush(_styleProvider.PanelBackgroundAlt),
                    ListItemStyle = new ImageTextButtonStyle
                    {
                        LabelStyle = new LabelStyle
                        {
                            Font = normalFont,
                            TextColor = _styleProvider.TextPrimary
                        },
                        OverBackground = new SolidBrush(_styleProvider.ListRowHover),
                        PressedBackground = new SolidBrush(_styleProvider.TextHighlight)
                    }
                }
            };

            // ListBox styles
            stylesheet.ListBoxStyle = new ListBoxStyle
            {
                ListItemStyle = new ImageTextButtonStyle
                {
                    LabelStyle = new LabelStyle
                    {
                        Font = normalFont,
                        TextColor = _styleProvider.TextPrimary
                    },
                    OverBackground = new SolidBrush(_styleProvider.ListRowHover),
                    PressedBackground = new SolidBrush(_styleProvider.TextHighlight)
                }
            };

            // CheckBox styles
            stylesheet.CheckBoxStyle = new ImageTextButtonStyle
            {
                LabelStyle = new LabelStyle
                {
                    Font = normalFont,
                    TextColor = _styleProvider.TextPrimary
                }
            };

            // RadioButton styles
            stylesheet.RadioButtonStyle = new ImageTextButtonStyle
            {
                LabelStyle = new LabelStyle
                {
                    Font = normalFont,
                    TextColor = _styleProvider.TextPrimary
                }
            };

            // TabControl styles
            stylesheet.TabControlStyle = new TabControlStyle
            {
                TabItemStyle = new ImageTextButtonStyle
                {
                    Background = new SolidBrush(_styleProvider.TabInactive),
                    LabelStyle = new LabelStyle
                    {
                        Font = normalFont,
                        TextColor = _styleProvider.TabText
                    }
                },
                ContentStyle = new WidgetStyle
                {
                    Background = new SolidBrush(_styleProvider.PanelBackgroundAlt)
                }
            };

            // ProgressBar styles
            stylesheet.HorizontalProgressBarStyle = new ProgressBarStyle
            {
                Background = new SolidBrush(_styleProvider.ProgressBarBackground),
                Filler = new SolidBrush(_styleProvider.ProgressBarFill)
            };

            stylesheet.VerticalProgressBarStyle = new ProgressBarStyle
            {
                Background = new SolidBrush(_styleProvider.ProgressBarBackground),
                Filler = new SolidBrush(_styleProvider.ProgressBarFill)
            };

            // SpinButton styles
            stylesheet.SpinButtonStyle = new SpinButtonStyle
            {
                TextBoxStyle = new TextBoxStyle
                {
                    Font = normalFont,
                    TextColor = _styleProvider.InputText,
                    Background = new SolidBrush(_styleProvider.InputBackground)
                }
            };

            // Separator styles
            stylesheet.HorizontalSeparatorStyle = new SeparatorStyle
            {
                Background = new SolidBrush(_styleProvider.PanelBorder)
            };

            stylesheet.VerticalSeparatorStyle = new SeparatorStyle
            {
                Background = new SolidBrush(_styleProvider.PanelBorder)
            };

            // Slider styles
            stylesheet.HorizontalSliderStyle = new SliderStyle
            {
                Background = new SolidBrush(_styleProvider.StatusBarBackground),
                Height = 20,
                KnobStyle = new ImageButtonStyle
                {
                    Background = new SolidBrush(_styleProvider.ButtonNormal),
                    OverBackground = new SolidBrush(_styleProvider.ButtonHover),
                    PressedBackground = new SolidBrush(_styleProvider.ButtonPressed),
                    Border = new SolidBrush(_styleProvider.ButtonBorder),
                    BorderThickness = new Thickness(1),
                    Width = 16,
                    Height = 20
                }
            };

            stylesheet.VerticalSliderStyle = new SliderStyle
            {
                Background = new SolidBrush(_styleProvider.StatusBarBackground),
                Width = 20,
                KnobStyle = new ImageButtonStyle
                {
                    Background = new SolidBrush(_styleProvider.ButtonNormal),
                    OverBackground = new SolidBrush(_styleProvider.ButtonHover),
                    PressedBackground = new SolidBrush(_styleProvider.ButtonPressed),
                    Border = new SolidBrush(_styleProvider.ButtonBorder),
                    BorderThickness = new Thickness(1),
                    Width = 20,
                    Height = 16
                }
            };

            // SplitPane styles (minimal — will be refined when first used)
            stylesheet.HorizontalSplitPaneStyle = new SplitPaneStyle();
            stylesheet.VerticalSplitPaneStyle = new SplitPaneStyle();

            // Menu styles
            stylesheet.HorizontalMenuStyle = new MenuStyle
            {
                LabelStyle = new LabelStyle
                {
                    Font = normalFont,
                    TextColor = _styleProvider.TextPrimary
                }
            };

            stylesheet.VerticalMenuStyle = new MenuStyle
            {
                LabelStyle = new LabelStyle
                {
                    Font = normalFont,
                    TextColor = _styleProvider.TextPrimary
                }
            };

            // Tree styles
            stylesheet.TreeStyle = new TreeStyle
            {
                LabelStyle = new LabelStyle
                {
                    Font = normalFont,
                    TextColor = _styleProvider.TextPrimary
                }
            };

            return stylesheet;
        }
    }
}
