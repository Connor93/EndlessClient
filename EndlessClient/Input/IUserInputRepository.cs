using AutomaticTypeMapper;
using Microsoft.Xna.Framework.Input;

namespace EndlessClient.Input
{
    public interface IUserInputRepository
    {
        KeyboardState PreviousKeyState { get; set; }

        KeyboardState CurrentKeyState { get; set; }

        MouseState PreviousMouseState { get; set; }

        MouseState CurrentMouseState { get; set; }

        /// <summary>
        /// Set to true when a UI component consumes the scroll wheel input.
        /// Reset at the start of each frame by CurrentUserInputTracker.
        /// </summary>
        bool ScrollWheelConsumed { get; set; }
    }

    public interface IUserInputProvider
    {
        KeyboardState PreviousKeyState { get; }

        KeyboardState CurrentKeyState { get; }

        MouseState PreviousMouseState { get; }

        MouseState CurrentMouseState { get; }

        /// <summary>
        /// Returns true if a UI component has consumed the scroll wheel input this frame.
        /// </summary>
        bool ScrollWheelConsumed { get; }
    }

    [MappedType(BaseType = typeof(IUserInputRepository), IsSingleton = true)]
    [MappedType(BaseType = typeof(IUserInputProvider), IsSingleton = true)]
    public class KeyStateRepository : IUserInputRepository, IUserInputProvider
    {
        public KeyboardState PreviousKeyState { get; set; }

        public KeyboardState CurrentKeyState { get; set; }

        public MouseState PreviousMouseState { get; set; }

        public MouseState CurrentMouseState { get; set; }

        public bool ScrollWheelConsumed { get; set; }
    }
}
