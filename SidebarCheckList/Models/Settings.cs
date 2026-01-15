using System.Text.Json.Serialization;

namespace SidebarChecklist.Models
{
    public sealed class SettingsRoot
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("api")]
        public ApiSettings Api { get; set; } = new();

        [JsonPropertyName("window")]
        public WindowSettings Window { get; set; } = new();

        [JsonPropertyName("display")]
        public DisplaySettings Display { get; set; } = new();

        [JsonPropertyName("selection")]
        public SelectionSettings Selection { get; set; } = new();

        [JsonPropertyName("checklist")]
        public ChecklistSettings Checklist { get; set; } = new();
    }

    public sealed class WindowSettings
    {
        [JsonPropertyName("sidebar_width_px")]
        public int SidebarWidthPx { get; set; } = 400;
    }

    public sealed class DisplaySettings
    {
        // "main" or "sub"
        [JsonPropertyName("target_monitor")]
        public string TargetMonitor { get; set; } = "main";
    }

    public sealed class SelectionSettings
    {
        [JsonPropertyName("selected_list_id")]
        public string SelectedListId { get; set; } = "mail_send";
    }

    public sealed class ChecklistSettings
    {
        [JsonPropertyName("font_size")]
        public int FontSize { get; set; } = 14;

        [JsonPropertyName("checkbox_size")]
        public int CheckboxSize { get; set; } = 16;
    }

    public sealed class ApiSettings
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("base_url")]
        public string BaseUrl { get; set; } = "";

        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = "";

        [JsonPropertyName("timeout_ms")]
        public int TimeoutMs { get; set; } = 3000;
    }
}
