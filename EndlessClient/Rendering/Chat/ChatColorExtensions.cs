using System;
using EndlessClient.UI.Styles;
using EOLib.Domain.Chat;
using Microsoft.Xna.Framework;

namespace EndlessClient.Rendering.Chat
{
    public static class ChatColorExtensions
    {
        public static Color ToColor(this ChatColor chatColor, IUIStyleProvider styleProvider)
        {
            switch (chatColor)
            {
                case ChatColor.Default: return styleProvider.ChatDefault;
                case ChatColor.Error: return styleProvider.ChatError;
                case ChatColor.PM: return styleProvider.ChatPM;
                case ChatColor.Server: return styleProvider.ChatServer;
                case ChatColor.ServerGlobal: return styleProvider.ChatServerGlobal;
                case ChatColor.Admin: return styleProvider.ChatAdmin;
                default: throw new ArgumentOutOfRangeException(nameof(chatColor), chatColor, "Unrecognized chat color");
            }
        }
    }
}
