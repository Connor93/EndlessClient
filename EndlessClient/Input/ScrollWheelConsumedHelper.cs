using AutomaticTypeMapper;

namespace EndlessClient.Input
{
    /// <summary>
    /// Static helper for marking scroll wheel events as consumed.
    /// Used by scrollbars to prevent MapRenderer zoom while scrolling UI.
    /// </summary>
    public static class ScrollWheelConsumedHelper
    {
        private static IUserInputRepository _repository;

        /// <summary>
        /// Initialize the helper with the user input repository.
        /// Called once during game startup.
        /// </summary>
        public static void Initialize(IUserInputRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Mark the scroll wheel as consumed for this frame.
        /// Should be called by any UI component that handles scroll input.
        /// </summary>
        public static void MarkConsumed()
        {
            if (_repository != null)
                _repository.ScrollWheelConsumed = true;
        }
    }
}
