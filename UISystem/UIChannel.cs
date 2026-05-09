namespace AK.UISystem
{
    /*
    HUD Channel: For persistent UI that should always remain visible and interactive during gameplay.
    Menu Channel: For standard, full-screen menus and popups that follow typical navigation rules (e.g., opening a new menu pauses the one behind it).
    Overlay Channel: For high-priority, transient notifications that appear on top of everything else without interrupting the UI below.
     */
    public enum UIChannel
    {
        HUD     = 0,
        Menu    = 100,
        Overlay = 200
    }
}