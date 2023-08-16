using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

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

            //this.FindControl<DataGrid>("DataGrid").Items = x;

            SidebarGridSplitter.DragDelta += SidebarGridSplitter_DragDelta;
        }

        private void SidebarGridSplitter_DragDelta(object? sender, VectorEventArgs e)
        {
            // how do I calculate the sidebar width from the mouse drag position?

            var tl = TopLevel.GetTopLevel(this);
            //var m = SidebarGridSplitter.GetTransformedBounds();
            //var x = SidebarGridSplitter.PointToScreen(Point.) + e.Vector.X;
            Debug.Print($"WindowWidth={this.Width.ToString()} GetTransformedBounds={SidebarGridSplitter.GetTransformedBounds().Value} Vector={e.Vector.X.ToString()}");
            //var calc = this.Width + SidebarGridSplitter.PointToClient(PixelPoint.Origin).X;
            //Debug.Print(calc.ToString());
            DragWidth.Text  = $"{SidebarGridSplitter.Bounds.Left + e.Vector.X}";
            DragWidthMinus.Text  = $"{this.Bounds.Width - (SidebarGridSplitter.Bounds.Left + e.Vector.X)}";
        }
        public class Person
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }
    }
}
