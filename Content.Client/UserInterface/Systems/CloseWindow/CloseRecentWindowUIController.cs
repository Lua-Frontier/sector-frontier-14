using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.UserInterface.Systems.Info;

public sealed class CloseRecentWindowUIController : UIController
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    /// <summary>
    /// A list of windows that have been interacted with recently.  Windows should only
    /// be in this list once, with the most recent window at the end, and the oldest
    /// window at the start.
    /// </summary>
    private readonly List<BaseWindow> _recentlyInteractedWindows = new();

    public override void Initialize()
    {
        // Add listeners to be able to know when windows are opened.
        // (Does not need to be unlistened since UIControllers live forever)
        _uiManager.OnKeyBindDown += OnKeyBindDown;
        _uiManager.WindowRoot.OnChildAdded += OnRootChildAdded;
        _uiManager.WindowRoot.OnChildRemoved += OnRootChildRemoved;

        _inputManager.SetInputCommand(EngineKeyFunctions.WindowCloseRecent,
            InputCmdHandler.FromDelegate(session => CloseMostRecentWindow()));
    }

    /// <summary>
    /// Closes the most recently focused window.
    /// </summary>
    public void CloseMostRecentWindow()
    {
        // Search backwards through the recency list to find a still open window and close it
        for (var i = _recentlyInteractedWindows.Count - 1; i >= 0; i--)
        {
            var window = _recentlyInteractedWindows[i];
            _recentlyInteractedWindows.RemoveAt(i); // Should always be removed as either the reference is stale or we're closing it
            if (window.IsOpen)
            {
                window.Close();
                return;
            }
            // continue going down the list, hoping to find a still-open window
        }
    }

    private void OnKeyBindDown(Control control)
    {
        // On click, we should set the window that owns this control (if any) to the most recently
        // clicked window.  By doing this, we can create an ordering of what windows have been
        // interacted with.

        // Something was clicked, so find the window corresponding to what was clicked
        var window = GetWindowForControl(control);

        // Find the window owning the control
        if (window != null)
        {
            // And move to top of recent stack
            SetMostRecentlyInteractedWindow(window);
        }
    }

    /// <summary>
    /// Sets the window as the one most recently interacted with.  This function will update the
    /// internal recentlyInteractedWindows tracking.
    /// </summary>
    public void SetMostRecentlyInteractedWindow(BaseWindow window)
    {
        // Search through the list and see if already added.
        // (This search is backwards since it's fairly common that the user is clicking the same
        // window multiple times in a row, and so that saves a tiny bit of perf doing it this way)
        for (var i = _recentlyInteractedWindows.Count - 1; i >= 0; i--)
        {
            if (_recentlyInteractedWindows[i] != window)
                continue;

            // Window already in the list

            // Is window the top most recent entry?
            if (i == _recentlyInteractedWindows.Count - 1)
                return; // Then there's nothing to do, it's already in the right spot

            // Need to remove the old entry so it can be readded (no duplicates in list allowed)
            _recentlyInteractedWindows.RemoveAt(i);
            break;
        }

        // Now that the list has been checked for duplicates, okay to add new window at end of tracking
        _recentlyInteractedWindows.Add(window);
    }

    private static BaseWindow? GetWindowForControl(Control? control)
    {
        if (control == null)
            return null;

        if (control is BaseWindow window)
            return window;

        // Go up the hierarchy until we find a window (or don't)
        return GetWindowForControl(control.Parent);
    }

    private void OnRootChildAdded(Control control)
    {
        if (control is BaseWindow window)
        {
            // On new window open, add to tracking
            SetMostRecentlyInteractedWindow(window);
        }
    }

    private void OnRootChildRemoved(Control control)
    {
        if (control is not BaseWindow window)
            return;

        _recentlyInteractedWindows.Remove(window);
    }

    /// <summary>
    /// Checks whether there are any windows that can be closed.
    /// </summary>
    public bool HasClosableWindow()
    {
        for (var i = _recentlyInteractedWindows.Count - 1; i >= 0; i--)
        {
            var window = _recentlyInteractedWindows[i];
            if (window.IsOpen)
                return true;

            _recentlyInteractedWindows.RemoveAt(i);
        }

        return false;
    }

    public int TrackedWindowCount => _recentlyInteractedWindows.Count;
}
