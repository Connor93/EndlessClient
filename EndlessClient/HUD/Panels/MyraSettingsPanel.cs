using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Dialogs;
using EndlessClient.Dialogs.Factories;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Domain.Chat;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Myra-based Settings panel. Shows a 2-column × 6-row grid of clickable settings
    /// that cycle through their values (matching CodeDrawnSettingsPanel behavior).
    /// </summary>
    public class MyraSettingsPanel : MyraHudPanelBase
    {
        private enum WhichSetting
        {
            Sfx, Mfx, Keyboard, Language, HearWhispers,
            ShowBalloons, ShowShadows, CurseFilter,
            LogChat, Interaction, MapZoom, ScrollWheelZoom,
        }

        private enum KeyboardLayout { English, Dutch, Swedish, Azerty }

        private readonly IChatActions _chatActions;
        private readonly IAudioActions _audioActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IConfigurationRepository _configurationRepository;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IConfigFileSaveActions _configFileSaveActions;
        private readonly IMyraFontProvider _fontProvider;

        private readonly Dictionary<WhichSetting, Label> _valueLabels = new();
        private bool _soundChanged, _musicChanged;
        private KeyboardLayout _keyboardLayout;

        private static readonly Color EnabledGreen = new(0x22, 0xAA, 0x44);
        private static readonly Color DisabledRed = new(0xCC, 0x44, 0x44);
        private static readonly Color LabelDim = new(0x88, 0x88, 0x99);

        private static readonly Dictionary<WhichSetting, string> SettingNames = new()
        {
            { WhichSetting.Sfx, "Sound" },
            { WhichSetting.Mfx, "Music" },
            { WhichSetting.Keyboard, "Keyboard" },
            { WhichSetting.Language, "Language" },
            { WhichSetting.HearWhispers, "Whispers" },
            { WhichSetting.ShowBalloons, "Balloons" },
            { WhichSetting.ShowShadows, "Shadows" },
            { WhichSetting.CurseFilter, "Filter" },
            { WhichSetting.LogChat, "Log Chat" },
            { WhichSetting.Interaction, "Interaction" },
            { WhichSetting.MapZoom, "Map Zoom" },
            { WhichSetting.ScrollWheelZoom, "Scroll Zoom" },
        };

        public MyraSettingsPanel(Game game,
                                 IMyraUIManager uiManager,
                                 IMyraFontProvider fontProvider,
                                 IChatActions chatActions,
                                 IAudioActions audioActions,
                                 ILocalizedStringFinder localizedStringFinder,
                                 IEOMessageBoxFactory messageBoxFactory,
                                 IConfigurationRepository configurationRepository,
                                 ISfxPlayer sfxPlayer,
                                 IConfigFileSaveActions configFileSaveActions)
            : base(game, uiManager, "Settings")
        {
            _fontProvider = fontProvider;
            _chatActions = chatActions;
            _audioActions = audioActions;
            _localizedStringFinder = localizedStringFinder;
            _messageBoxFactory = messageBoxFactory;
            _configurationRepository = configurationRepository;
            _sfxPlayer = sfxPlayer;
            _configFileSaveActions = configFileSaveActions;
        }

        public override void Initialize()
        {
            Window.Width = 480;
            Window.Height = 178;
            Window.TitleFont = _fontProvider.Large;

            // Single flat grid: 6 columns (label|value|arrow × 2), 6 rows
            var grid = new Grid
            {
                ColumnSpacing = 4,
                RowSpacing = 1,
                Padding = new Thickness(4),
            };

            // Column layout: label1 | value1 | arrow1 | gap | label2 | value2 | arrow2
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 72));  // label left
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 72));  // value left
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 24));  // arrow left
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 24));  // gap
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 72));  // label right
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 72));  // value right
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 24));  // arrow right

            for (int i = 0; i < 6; i++)
                grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            var settings = Enum.GetValues<WhichSetting>();
            foreach (var setting in settings)
            {
                var ndx = (int)setting;
                var half = ndx / 6;       // 0 = left column, 1 = right column
                var row = ndx % 6;
                var colBase = half * 4;   // 0 for left, 4 for right (skip gap col)

                // Setting label
                var nameLabel = new Label
                {
                    Text = SettingNames[setting],
                    Font = _fontProvider.Normal,
                    TextColor = LabelDim,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(nameLabel, colBase);
                Grid.SetRow(nameLabel, row);
                grid.Widgets.Add(nameLabel);

                // Value label
                var valueLabel = new Label
                {
                    Text = "",
                    Font = _fontProvider.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _valueLabels[setting] = valueLabel;
                Grid.SetColumn(valueLabel, colBase + 1);
                Grid.SetRow(valueLabel, row);
                grid.Widgets.Add(valueLabel);

                // Arrow indicator
                var arrowLabel = new Label
                {
                    Text = "◄►",
                    Font = _fontProvider.Small,
                    TextColor = LabelDim,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(arrowLabel, colBase + 2);
                Grid.SetRow(arrowLabel, row);
                grid.Widgets.Add(arrowLabel);

                // Invisible click target spanning the 3 cells
                var clickTarget = new Panel
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Height = 22,
                };
                var capturedSetting = setting;
                clickTarget.TouchDown += (_, _) => SettingChange(capturedSetting);
                Grid.SetColumn(clickTarget, colBase);
                Grid.SetColumnSpan(clickTarget, 3);
                Grid.SetRow(clickTarget, row);
                grid.Widgets.Add(clickTarget);
            }

            Window.Content = grid;

            // Apply whisper setting on init (matches CodeDrawn behavior)
            if (!_configurationRepository.HearWhispers)
                _chatActions.SetHearWhispers(_configurationRepository.HearWhispers);

            UpdateAllDisplayText();
            base.Initialize();
        }

        private void UpdateAllDisplayText()
        {
            SetValue(WhichSetting.Sfx, _configurationRepository.SoundVolume switch
            {
                100 => _localizedStringFinder.GetString(EOResourceID.SETTING_ENABLED),
                0 => _localizedStringFinder.GetString(EOResourceID.SETTING_DISABLED),
                _ => $"{_configurationRepository.SoundVolume}%"
            });
            SetValue(WhichSetting.Mfx, _localizedStringFinder.GetString(
                _configurationRepository.MusicEnabled ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED));
            SetValue(WhichSetting.Keyboard, _localizedStringFinder.GetString(EOResourceID.SETTING_KEYBOARD_ENGLISH));
            SetValue(WhichSetting.Language, _localizedStringFinder.GetString(EOResourceID.SETTING_LANG_CURRENT));
            SetValue(WhichSetting.HearWhispers, _localizedStringFinder.GetString(
                _configurationRepository.HearWhispers ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED));
            SetValue(WhichSetting.ShowBalloons, _localizedStringFinder.GetString(
                _configurationRepository.ShowChatBubbles ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED));
            SetValue(WhichSetting.ShowShadows, _localizedStringFinder.GetString(
                _configurationRepository.ShowShadows ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED));

            if (_configurationRepository.StrictFilterEnabled)
                SetValue(WhichSetting.CurseFilter, _localizedStringFinder.GetString(EOResourceID.SETTING_EXCLUSIVE));
            else if (_configurationRepository.CurseFilterEnabled)
                SetValue(WhichSetting.CurseFilter, _localizedStringFinder.GetString(EOResourceID.SETTING_NORMAL));
            else
                SetValue(WhichSetting.CurseFilter, _localizedStringFinder.GetString(EOResourceID.SETTING_DISABLED));

            SetValue(WhichSetting.LogChat, _localizedStringFinder.GetString(
                _configurationRepository.LogChatToFile ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED));
            SetValue(WhichSetting.Interaction, _localizedStringFinder.GetString(
                _configurationRepository.Interaction ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED));
            SetValue(WhichSetting.MapZoom, $"{(int)(_configurationRepository.MapZoom * 100)}%");
            SetValue(WhichSetting.ScrollWheelZoom, _localizedStringFinder.GetString(
                _configurationRepository.ScrollWheelZoom ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED));
        }

        private void SetValue(WhichSetting setting, string value)
        {
            if (!_valueLabels.TryGetValue(setting, out var label))
                return;

            label.Text = value;

            var enabledStr = _localizedStringFinder.GetString(EOResourceID.SETTING_ENABLED);
            var disabledStr = _localizedStringFinder.GetString(EOResourceID.SETTING_DISABLED);

            if (value == enabledStr)
                label.TextColor = EnabledGreen;
            else if (value == disabledStr)
                label.TextColor = DisabledRed;
            else
                label.TextColor = Color.White;
        }

        private void SettingChange(WhichSetting setting)
        {
            _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);

            switch (setting)
            {
                case WhichSetting.Sfx:
                    {
                        var nextVolume = _configurationRepository.SoundVolume switch
                        {
                            100 => 50,
                            50 => 0,
                            _ => 100
                        };

                        if (!_soundChanged && _configurationRepository.SoundVolume == 0 && nextVolume > 0)
                        {
                            var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SETTINGS_SOUND_DISABLED, EODialogButtons.OkCancel);
                            dlg.DialogClosing += (_, e) =>
                            {
                                if (e.Result != XNADialogResult.OK) return;
                                _soundChanged = true;
                                _configurationRepository.SoundVolume = nextVolume;
                                _audioActions.ToggleSound();
                                UpdateAllDisplayText();
                            };
                            dlg.ShowDialog();
                        }
                        else
                        {
                            _soundChanged = true;
                            _configurationRepository.SoundVolume = nextVolume;
                            _audioActions.ToggleSound();
                        }
                    }
                    break;
                case WhichSetting.Mfx:
                    {
                        if (!_musicChanged && !_configurationRepository.MusicEnabled)
                        {
                            var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SETTINGS_MUSIC_DISABLED, EODialogButtons.OkCancel);
                            dlg.DialogClosing += (_, e) =>
                            {
                                if (e.Result != XNADialogResult.OK) return;
                                _musicChanged = true;
                                _configurationRepository.MusicEnabled = !_configurationRepository.MusicEnabled;
                                _audioActions.ToggleBackgroundMusic();
                                UpdateAllDisplayText();
                            };
                            dlg.ShowDialog();
                        }
                        else
                        {
                            _musicChanged = true;
                            _configurationRepository.MusicEnabled = !_configurationRepository.MusicEnabled;
                            _audioActions.ToggleBackgroundMusic();
                        }
                    }
                    break;
                case WhichSetting.Keyboard:
                    _keyboardLayout++;
                    if (_keyboardLayout > KeyboardLayout.Azerty)
                        _keyboardLayout = 0;
                    break;
                case WhichSetting.Language:
                    _configurationRepository.Language++;
                    if (_configurationRepository.Language > EOLanguage.Portuguese)
                        _configurationRepository.Language = 0;
                    break;
                case WhichSetting.HearWhispers:
                    _configurationRepository.HearWhispers = !_configurationRepository.HearWhispers;
                    _chatActions.SetHearWhispers(_configurationRepository.HearWhispers);
                    break;
                case WhichSetting.ShowBalloons:
                    _configurationRepository.ShowChatBubbles = !_configurationRepository.ShowChatBubbles;
                    break;
                case WhichSetting.ShowShadows:
                    _configurationRepository.ShowShadows = !_configurationRepository.ShowShadows;
                    break;
                case WhichSetting.CurseFilter:
                    if (_configurationRepository.StrictFilterEnabled)
                    {
                        _configurationRepository.StrictFilterEnabled = false;
                    }
                    else if (_configurationRepository.CurseFilterEnabled)
                    {
                        _configurationRepository.CurseFilterEnabled = false;
                        _configurationRepository.StrictFilterEnabled = true;
                    }
                    else
                    {
                        _configurationRepository.CurseFilterEnabled = true;
                    }
                    break;
                case WhichSetting.LogChat:
                    _configurationRepository.LogChatToFile = !_configurationRepository.LogChatToFile;
                    break;
                case WhichSetting.Interaction:
                    _configurationRepository.Interaction = !_configurationRepository.Interaction;
                    break;
                case WhichSetting.MapZoom:
                    {
                        var zoomLevels = new[] { 1.0f, 1.25f, 1.5f, 1.75f, 2.0f };
                        var currentZoom = _configurationRepository.MapZoom;
                        var currentIndex = Array.FindIndex(zoomLevels, z => Math.Abs(z - currentZoom) < 0.01f);
                        if (currentIndex == -1) currentIndex = 0;
                        var nextIndex = (currentIndex + 1) % zoomLevels.Length;
                        _configurationRepository.MapZoom = zoomLevels[nextIndex];
                    }
                    break;
                case WhichSetting.ScrollWheelZoom:
                    _configurationRepository.ScrollWheelZoom = !_configurationRepository.ScrollWheelZoom;
                    break;
            }

            UpdateAllDisplayText();
            _configFileSaveActions.SaveConfigFile();
        }
    }
}
