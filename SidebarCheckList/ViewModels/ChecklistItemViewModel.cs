namespace SidebarChecklist.ViewModels
{
    public sealed class ChecklistItemViewModel
    {
        public string Text { get; }
        public bool IsChecked { get; set; } // 永続化しない（将来検討）

        public ChecklistItemViewModel(string text)
        {
            Text = text ?? "";
        }
    }
}
