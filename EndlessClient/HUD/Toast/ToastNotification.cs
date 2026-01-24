using System;
using System.Diagnostics;

namespace EndlessClient.HUD.Toast
{
    /// <summary>
    /// Type of toast notification for color-coding
    /// </summary>
    public enum ToastType
    {
        /// <summary>General information, button hovers, item/spell descriptions</summary>
        Info,
        /// <summary>Warnings, errors, can't do that messages</summary>
        Warning,
        /// <summary>Actions taken - XP gained, items picked up, panels switched</summary>
        Action
    }

    /// <summary>
    /// Individual toast notification data
    /// </summary>
    public class ToastNotification
    {
        /// <summary>The text message to display</summary>
        public string Message { get; }

        /// <summary>Type of notification for color selection</summary>
        public ToastType Type { get; }

        /// <summary>Timer for animation and auto-dismiss</summary>
        public Stopwatch Timer { get; }

        /// <summary>Current animation progress (0 = sliding in, 1 = fully visible)</summary>
        public float SlideProgress { get; set; }

        /// <summary>Fade out progress (0 = fully visible, 1 = faded out)</summary>
        public float FadeProgress { get; set; }

        /// <summary>Whether this toast is marked for removal</summary>
        public bool IsExpired { get; set; }

        public ToastNotification(string message, ToastType type)
        {
            Message = message;
            Type = type;
            Timer = Stopwatch.StartNew();
            SlideProgress = 0;
            FadeProgress = 0;
            IsExpired = false;
        }
    }
}
