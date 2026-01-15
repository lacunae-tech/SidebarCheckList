using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SidebarChecklist.Models
{
    public sealed class ChecklistSaveEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = "";

        [JsonPropertyName("items")]
        public List<ChecklistSavedItem> Items { get; set; } = new();

        [JsonPropertyName("checklist_version")]
        public string ChecklistVersion { get; set; } = "";
    }

    public sealed class ChecklistSavedItem
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("is_checked")]
        public bool IsChecked { get; set; }
    }
}
