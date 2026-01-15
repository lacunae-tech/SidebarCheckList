namespace SidebarChecklist.Models
{
    public sealed class SettingsRoot
    {
        public string Version { get; set; } = "1.0";
        public WindowSettings Window { get; set; } = new();
        public DisplaySettings Display { get; set; } = new();
        public SelectionSettings Selection { get; set; } = new();
    }

    public sealed class WindowSettings
    {
        public int SidebarWidthPx { get; set; } = 400;
    }

    public sealed class DisplaySettings
    {
        // "main" or "sub"
        public string TargetMonitor { get; set; } = "main";
    }

    public sealed class SelectionSettings
    {
        public string SelectedListId { get; set; } = "mail_send";
    }
}
