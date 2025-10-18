using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.Library.Config;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels.AddItem.Pages
{
    public class MockResultsViewModel : ResultsViewModel
    {
        public MockResultsViewModel() : base(CreateMockAddItemViewModel(), CreateMockLibrary())
        {
            // Set mock search term
            SearchTerm = "";
            
            SelectedLibraryItem = Library.Items.First();
        }

        private static AddItemViewModel CreateMockAddItemViewModel()
        {
            // Create a minimal mock AddItemViewModel for design time
            // This might need adjustment based on AddItemViewModel's constructor requirements
            return null; // Placeholder - may need actual implementation
        }

        private static Library CreateMockLibrary()
        {
            // Create mock library
            var library = new Library(new LibraryConfig.LibraryDefinition
            {
                Label = "Sample Song Library",
                Directory = "/Library"
            });

            // Add mock items to the library
            var mockItems = new List<LibraryItem>
            {
                new LibraryItem { FullFilePath = "/Library/Amazing Grace.txt" },
                new LibraryItem { FullFilePath = "/Library/How Great Is Our God.txt" },
                new LibraryItem { FullFilePath = "/Library/10,000 Reasons.txt" },
                new LibraryItem { FullFilePath = "/Library/Blessed Be Your Name.txt" },
                new LibraryItem { FullFilePath = "/Library/Here I Am to Worship.txt" },
                new LibraryItem { FullFilePath = "/Library/In Christ Alone.txt" },
                new LibraryItem { FullFilePath = "/Library/Mighty to Save.txt" },
                new LibraryItem { FullFilePath = "/Library/Our God.txt" },
                new LibraryItem { FullFilePath = "/Library/What a Beautiful Name.txt" },
                new LibraryItem { FullFilePath = "/Library/Reckless Love.txt" }
            };

            // Add items to library's Items collection
            foreach (var item in mockItems)
            {
                library.Items.Add(item);
            }

            return library;
        }
    }
}
