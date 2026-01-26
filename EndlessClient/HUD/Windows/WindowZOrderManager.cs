using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticTypeMapper;

namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Manages z-ordering for code-drawn windows.
    /// Windows register with this manager and can be brought to front when activated.
    /// </summary>
    public interface IWindowZOrderManager
    {
        /// <summary>
        /// Registers a window with the z-order manager.
        /// </summary>
        void Register(IZOrderedWindow window);

        /// <summary>
        /// Unregisters a window from the z-order manager.
        /// </summary>
        void Unregister(IZOrderedWindow window);

        /// <summary>
        /// Gets the next available z-order value (current max + 1).
        /// </summary>
        int GetNextZOrder();

        /// <summary>
        /// Brings the specified window to the front of all registered windows.
        /// </summary>
        void BringToFront(IZOrderedWindow window);
    }

    [AutoMappedType(IsSingleton = true)]
    public class WindowZOrderManager : IWindowZOrderManager
    {
        private const int BaseWindowZOrder = 100;
        private readonly List<IZOrderedWindow> _windows = new List<IZOrderedWindow>();
        private int _maxZOrder = BaseWindowZOrder;

        public void Register(IZOrderedWindow window)
        {
            if (!_windows.Contains(window))
            {
                _windows.Add(window);
                // Assign unique initial z-order to each window
                window.ZOrder = GetNextZOrder();
            }
        }

        public void Unregister(IZOrderedWindow window)
        {
            _windows.Remove(window);
        }

        public int GetNextZOrder()
        {
            return ++_maxZOrder;
        }

        public void BringToFront(IZOrderedWindow window)
        {
            if (_windows.Contains(window))
            {
                window.ZOrder = GetNextZOrder();
            }
        }
    }
}
