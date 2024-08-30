using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Models.PlaylistActions;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views
{
    public partial class AddItemRoot : UserControl
    {
        public AddItemRoot()
        {
            InitializeComponent();
        }

    }
}