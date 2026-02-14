using System;
using EOLib.Domain.Chat;
using EOLib.Graphics;
using Microsoft.Xna.Framework;

namespace EndlessClient.Rendering.Chat
{
    public static class ChatColorExtensions
    {
        public static Color ToColor(this ChatColor chatColor)
        {
            switch (chatColor)
            {
                case ChatColor.Default: return Color.Black;
                case ChatColor.Error: return Color.FromNonPremultiplied(0x7d, 0x0a, 0x0a, 0xff);
                case ChatColor.PM: return Color.FromNonPremultiplied(0x5a, 0x3c, 0x00, 0xff);
                case ChatColor.Server: return Color.FromNonPremultiplied(0x8a, 0x5c, 0x4a, 0xff);
                case ChatColor.ServerGlobal: return Color.FromNonPremultiplied(0x8a, 0x6d, 0x00, 0xff);
                case ChatColor.Admin: return Color.FromNonPremultiplied(0x7a, 0x4a, 0x2a, 0xff);
                default: throw new ArgumentOutOfRangeException(nameof(chatColor), chatColor, "Unrecognized chat color");
            }
        }
    }
}
