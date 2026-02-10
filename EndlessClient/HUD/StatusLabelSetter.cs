using System;
using AutomaticTypeMapper;
using EndlessClient.HUD.Toast;
using EOLib.Domain.Chat;
using EOLib.Localization;

namespace EndlessClient.HUD
{
    [AutoMappedType]
    public class StatusLabelSetter : IStatusLabelSetter
    {
        private readonly IStatusLabelTextRepository _statusLabelTextRepository;
        private readonly IChatRepository _chatRepository;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IToastNotifier _toastNotifier;

        public StatusLabelSetter(IStatusLabelTextRepository statusLabelTextRepository,
                                 IChatRepository chatRepository,
                                 ILocalizedStringFinder localizedStringFinder,
                                 IToastNotifier toastNotifier)
        {
            _statusLabelTextRepository = statusLabelTextRepository;
            _chatRepository = chatRepository;
            _localizedStringFinder = localizedStringFinder;
            _toastNotifier = toastNotifier;
        }

        public void SetStatusLabel(EOResourceID type, EOResourceID text, string appended = "", bool showChatError = false)
        {
            CheckStatusLabelType(type);

            SetStatusLabelText(_localizedStringFinder.GetString(type),
                               _localizedStringFinder.GetString(text),
                               appended,
                               showChatError);
        }

        public void SetStatusLabel(EOResourceID type, string prepended, EOResourceID text, bool showChatError = false)
        {
            CheckStatusLabelType(type);
            SetStatusLabelText(_localizedStringFinder.GetString(type),
                               prepended,
                               _localizedStringFinder.GetString(text),
                               showChatError);
        }

        public void SetStatusLabel(EOResourceID type, string text)
        {
            CheckStatusLabelType(type);
            SetStatusLabelText(_localizedStringFinder.GetString(type), text);
        }

        public void SetStatusLabel(string text)
        {
            _statusLabelTextRepository.StatusText = text;
            _statusLabelTextRepository.SetTime = DateTime.Now;
        }

        public void ShowWarning(string message)
        {
            SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_WARNING, message);
        }

        public void NotifyGuildBounty(string playerName, string bountyName, int guildPoints)
        {
            _toastNotifier.NotifyGuildBounty(playerName, bountyName, guildPoints);
        }

        private void SetStatusLabelText(string type, string text, string extra = "", bool showChatError = false)
        {
            _statusLabelTextRepository.StatusText = $"[ {type} ] {text}{extra}";
            _statusLabelTextRepository.SetTime = DateTime.Now;

            if (showChatError)
            {
                var chatData = new ChatData(ChatTab.System, string.Empty, $"{text}{extra}", ChatIcon.Error, ChatColor.Error);
                _chatRepository.AllChat[ChatTab.System].Add(chatData);
            }
        }

        private void CheckStatusLabelType(EOResourceID type)
        {
            switch (type)
            {
                case EOResourceID.STATUS_LABEL_TYPE_ACTION:
                case EOResourceID.STATUS_LABEL_TYPE_BUTTON:
                case EOResourceID.STATUS_LABEL_TYPE_INFORMATION:
                case EOResourceID.STATUS_LABEL_TYPE_WARNING:
                case EOResourceID.STATUS_LABEL_TYPE_ITEM:
                case EOResourceID.SKILLMASTER_WORD_SPELL:
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(type), "Use either ACTION, BUTTION, INFORMATION, WARNING, ITEM, or SPELL for this.");
            }
        }
    }
}
