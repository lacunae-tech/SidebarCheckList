using SidebarChecklist.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SidebarChecklist.ViewModels
{
    public sealed class ChecklistListViewModel
    {
        public string Id { get; }
        public string Name { get; }
        public List<string> Items { get; }

        public ChecklistListViewModel(string id, string name, List<string> items)
        {
            Id = id;
            Name = name;
            Items = items;
        }
    }

    public sealed class MainViewModel
    {
        public ObservableCollection<ChecklistListViewModel> Lists { get; } = new();
        public ObservableCollection<ChecklistItemViewModel> Items { get; } = new();

        public string BodyMessage { get; private set; } = "";

        public ChecklistListViewModel? SelectedList { get; private set; }

        public void SetMessage(string msg)
        {
            BodyMessage = msg ?? "";
            Lists.Clear();
            Items.Clear();
            SelectedList = null;
        }

        public void SetLists(IEnumerable<ChecklistList> lists, string? preferredListId)
        {
            Lists.Clear();
            Items.Clear();

            foreach (var l in lists)
            {
                Lists.Add(new ChecklistListViewModel(
                    l.Id ?? "",
                    l.Name ?? "",
                    (l.Items ?? new List<string>()).ToList()
                ));
            }

            // selected_list_id 不正 → 静かに先頭へ
            var selected = Lists.FirstOrDefault(x => x.Id == (preferredListId ?? "")) ?? Lists.FirstOrDefault();
            SelectedList = selected;

            RefreshItems();
        }

        public void SelectByIdOrFirst(string id)
        {
            SelectedList = Lists.FirstOrDefault(x => x.Id == id) ?? Lists.FirstOrDefault();
            RefreshItems();
        }

        public void RefreshItems()
        {
            Items.Clear();
            if (SelectedList == null) return;

            foreach (var t in SelectedList.Items)
                Items.Add(new ChecklistItemViewModel(t));
        }
    }
}
