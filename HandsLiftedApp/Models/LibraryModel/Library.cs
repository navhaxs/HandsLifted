using HandsLiftedApp.Comparer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace HandsLiftedApp.Models.LibraryModel
{
    public class Library
    {
        private ObservableCollection<Item> Items { get; }
        private string rootDirectory;

        public Library() { 
            Items = new ObservableCollection<Item>();
            rootDirectory = "C:\\VisionScreens\\TestSongs";

            Refresh();
        }

        void Refresh()
        {
            var files = Directory.GetFiles(rootDirectory, "*.*", SearchOption.AllDirectories)                         
                     .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.Ordinal));

            foreach (var f in files)
            {
                Items.Add(new Item() { Name = f });
            }
        }
    }

    class Item
    {
        public string Name { get; set; }
    }
}
