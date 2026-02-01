using Content.Client.Resources;
using Content.Shared.Popups.GridNameDisplay;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using System.Text;

namespace Content.Client.Popups.GridNameDisplay;

/// <summary>
/// Handles displaying grid names when a player enters a grid.
/// </summary>
public sealed class GridNameDisplaySystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private Font? _gridNameFont;
    private Control? _container;
    private Label? _nameLabel;
    private TimeSpan? _displayUntil;
    private TimeSpan? _fadeStartTime;

    // Fields for letter-by-letter animation
    private string _fullText = string.Empty;
    private int _currentLetterCount;
    private TimeSpan _nextLetterTime;
    // Fields for letter-by-letter fade-out
    private int _fadeOutLetterCount;
    private TimeSpan _nextFadeOutLetterTime;
    private StringBuilder _visibleText = new();

    // Animation parameters
    private const float DisplayDuration = 2.0f; // Total display duration in seconds after all letters appear
    private const float TimeBetweenLetters = 0.15f; // Time between each letter appearing in seconds
    private const float TimeBetweenFadeOutLetters = 0.15f; // Time between each letter disappearing in seconds

    // UI States
    private enum DisplayState
    {
        Hidden,
        LetterAnimation, // State for letter-by-letter fade-in
        Visible,
        FadingOutLetterByLetter // State for letter-by-letter fade-out
    }

    private DisplayState _currentState = DisplayState.Hidden;

    public override void Initialize()
    {
        base.Initialize();

        _gridNameFont = _resourceCache.GetFont("/Fonts/Helvetica/Helvetica-Bold.ttf", 20);

        SubscribeNetworkEvent<ShowGridNameEvent>(OnShowGridName);
    }

    /// <summary>
    /// Called when the client receives a grid name to display
    /// </summary>
    private void OnShowGridName(ShowGridNameEvent ev)
    {
        // Create the UI elements if they don't exist
        if (_container == null)
        {
            // Create a container that will span the full width of the screen
            var boxContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalAlignment = Control.HAlignment.Stretch,
                VerticalAlignment = Control.VAlignment.Top,
                Margin = new Thickness(0, 80, 0, 0),
            };

            // Create the label that will display the grid name
            _nameLabel = new Label
            {
                HorizontalAlignment = Control.HAlignment.Center,
                HorizontalExpand = true,
                FontColorOverride = Color.White,
                Align = Label.AlignMode.Center,
                Margin = new Thickness(0, 0, 500, 0),
                FontOverride = _gridNameFont,
            };

            boxContainer.AddChild(_nameLabel);

            // Get the game's user interface root where we want to display our text
            var uiRoot = _uiManager.RootControl;

            // Add the container to the UI
            uiRoot.AddChild(boxContainer);
            _container = boxContainer;
        }

        if (_nameLabel == null)
            return;

        // Store the full text that we'll animate letter by letter
        _fullText = ev.GridName;
        _currentLetterCount = 0;
        _fadeOutLetterCount = 0;
        _visibleText.Clear();

        // Initialize the label with empty text
        _nameLabel.Text = "";

        // Calculate display times
        var currentTime = _timing.CurTime;
        _nextLetterTime = currentTime;

        // We'll set _displayUntil when all letters have appeared

        // Reset state for new display
        _currentState = DisplayState.LetterAnimation;
        _container!.Visible = true;
        _container.ModulateSelfOverride = Color.White; // Full opacity for the container
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_container == null || !_container.Visible || _nameLabel == null)
            return;

        var currentTime = _timing.CurTime;

        switch (_currentState)
        {
            case DisplayState.LetterAnimation:
                if (currentTime >= _nextLetterTime && _currentLetterCount < _fullText.Length)
                {
                    // Add the next letter
                    _currentLetterCount++;
                    _nameLabel.Text = _fullText.Substring(0, _currentLetterCount);
                    _visibleText.Append(_fullText[_currentLetterCount - 1]);

                    // Set time for next letter
                    _nextLetterTime = currentTime + TimeSpan.FromSeconds(TimeBetweenLetters);
                }

                // Check if all letters have been displayed
                if (_currentLetterCount >= _fullText.Length)
                {
                    _currentState = DisplayState.Visible;

                    // Now that all letters are displayed, set the display duration
                    // Display for DisplayDuration seconds after all letters have appeared
                    _displayUntil = currentTime + TimeSpan.FromSeconds(DisplayDuration);
                    _fadeStartTime = _displayUntil;
                }
                break;

            case DisplayState.Visible:
                if (!_fadeStartTime.HasValue || currentTime < _fadeStartTime.Value)
                    break;

                // Time to start fading out letter by letter
                _currentState = DisplayState.FadingOutLetterByLetter;
                _nextFadeOutLetterTime = currentTime;
                break;

            case DisplayState.FadingOutLetterByLetter:
                if (!_displayUntil.HasValue)
                    return;

                if (currentTime >= _nextFadeOutLetterTime && _fadeOutLetterCount < _fullText.Length)
                {
                    // Remove the last letter (from right to left)
                    _fadeOutLetterCount++;

                    if (_fadeOutLetterCount >= _fullText.Length)
                    {
                        // All letters have faded out
                        _nameLabel.Text = "";
                    }
                    else
                    {
                        // Show only the remaining letters from the beginning
                        _nameLabel.Text = _fullText.Substring(0, _fullText.Length - _fadeOutLetterCount);
                    }

                    // Set time for next letter fadeout
                    _nextFadeOutLetterTime = currentTime + TimeSpan.FromSeconds(TimeBetweenFadeOutLetters);
                }

                // Check if all letters have faded out
                if (_fadeOutLetterCount >= _fullText.Length)
                {
                    // Hide the container
                    _container.Visible = false;
                    _currentState = DisplayState.Hidden;
                    _displayUntil = null;
                    _fadeStartTime = null;
                }
                break;
        }
    }
}
