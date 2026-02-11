using System.Globalization;
using AutomaticTypeMapper;
using EOLib.Shared;

namespace EOLib.Config
{
    [MappedType(BaseType = typeof(IConfigFileSaveActions))]
    public class ConfigFileSaveActions : IConfigFileSaveActions
    {
        private readonly IConfigurationProvider _configProvider;

        public ConfigFileSaveActions(IConfigurationProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public void SaveConfigFile()
        {
            var configFile = new IniReader(Constants.Default_Config_File);
            configFile.Load();

            SetValue(configFile, ConfigStrings.Settings, ConfigStrings.Music, _configProvider.MusicEnabled);
            SetValue(configFile, ConfigStrings.Settings, ConfigStrings.Sound, _configProvider.SoundEnabled);
            SetValue(configFile, ConfigStrings.Settings, ConfigStrings.ShowBaloons, _configProvider.ShowChatBubbles);
            SetValue(configFile, ConfigStrings.Settings, ConfigStrings.ShowShadows, _configProvider.ShowShadows);

            SetValue(configFile, ConfigStrings.Chat, ConfigStrings.Filter, _configProvider.CurseFilterEnabled);
            SetValue(configFile, ConfigStrings.Chat, ConfigStrings.FilterAll, _configProvider.StrictFilterEnabled);
            SetValue(configFile, ConfigStrings.Chat, ConfigStrings.HearWhisper, _configProvider.HearWhispers);
            SetValue(configFile, ConfigStrings.Chat, ConfigStrings.Interaction, _configProvider.Interaction);
            SetValue(configFile, ConfigStrings.Chat, ConfigStrings.LogChat, _configProvider.LogChatToFile);

            SetValue(configFile, ConfigStrings.LANGUAGE, ConfigStrings.Language, ((int)_configProvider.Language).ToString());

            SetValue(configFile, ConfigStrings.Custom, ConfigStrings.MapZoom, _configProvider.MapZoom.ToString("F2", CultureInfo.InvariantCulture));
            SetValue(configFile, ConfigStrings.Custom, ConfigStrings.ScrollWheelZoom, _configProvider.ScrollWheelZoom);

            configFile.Save();
        }

        private static void SetValue(IniReader reader, string section, string key, bool value)
        {
            SetValue(reader, section, key, value ? "on" : "off");
        }

        private static void SetValue(IniReader reader, string section, string key, string value)
        {
            if (!reader.Sections.ContainsKey(section))
            {
                reader.Sections.Add(section, new System.Collections.Generic.SortedList<string, string>());
            }

            reader.Sections[section][key] = value;
        }
    }
}
