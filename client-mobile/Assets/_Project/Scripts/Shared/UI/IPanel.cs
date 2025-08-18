namespace BlokusUnity.UI
{
    /// <summary>
    /// UI Panel interface for standardized panel behavior
    /// Migration Plan: UIArchitecture (패널 규격)
    /// </summary>
    public interface IPanel
    {
        /// <summary>
        /// Show the panel with animation
        /// </summary>
        void Show();

        /// <summary>
        /// Hide the panel with animation
        /// </summary>
        void Hide();

        /// <summary>
        /// Check if panel is currently visible
        /// </summary>
        bool IsVisible { get; }
    }
}