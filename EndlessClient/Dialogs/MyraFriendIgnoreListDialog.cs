using System.Collections.Generic;
using System.Linq;
using EndlessClient.UI.Myra;
using EOLib.Domain.Online;
using Microsoft.Xna.Framework;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Native Myra replacement for the XNA FriendIgnoreListDialog.
    /// Polls for online player changes and highlights matching names.
    /// </summary>
    public class MyraFriendIgnoreListDialog : MyraScrollingListDialog
    {
        private readonly IOnlinePlayerProvider _onlinePlayerProvider;
        private HashSet<OnlinePlayerInfo> _cachedOnlinePlayers;

        public MyraFriendIgnoreListDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IOnlinePlayerProvider onlinePlayerProvider,
            string title)
            : base(uiManager, fontProvider, title, width: 320, height: 280)
        {
            _onlinePlayerProvider = onlinePlayerProvider;
            _cachedOnlinePlayers = new HashSet<OnlinePlayerInfo>();
        }

        public override void Update(GameTime gameTime)
        {
            if (!_cachedOnlinePlayers.SetEquals(_onlinePlayerProvider.OnlinePlayers))
            {
                _cachedOnlinePlayers = _onlinePlayerProvider.OnlinePlayers.ToHashSet();

                ClearHighlights();
                HighlightItemsByName(_cachedOnlinePlayers.Select(x => x.Name).ToList());
            }

            base.Update(gameTime);
        }
    }
}
