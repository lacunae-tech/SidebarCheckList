using System.Collections.Generic;

namespace SidebarChecklist.Models
{
    public sealed class ChecklistRoot
    {
        public string Version { get; set; } = "1.0";
        public List<ChecklistList> Lists { get; set; } = new();
    }

    public sealed class ChecklistList
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> Items { get; set; } = new();
    }
}
