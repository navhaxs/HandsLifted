using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ScratchApp
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Person> People { get; }
        public Boolean IsChecked { get; set; } = false;

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;

            var x = new List<Person>
            {
                new Person() { FirstName = "A", LastName = "A" },
                new Person() { FirstName = "B", LastName = "B" }
            };
            People = new ObservableCollection<Person>(x);

            this.FindControl<DataGrid>("DataGrid").Items = x;

        }

        public class Person
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }
    }
}
