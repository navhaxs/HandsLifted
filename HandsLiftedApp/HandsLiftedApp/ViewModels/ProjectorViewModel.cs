using HandsLiftedApp.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Text;
using WebViewControl;

namespace HandsLiftedApp.ViewModels
{
    public class ProjectorViewModel : ViewModelBase
    {
        public string Text => @"I wait upon You now
Lord I wait upon You now
Let Your presence fill me now
As I wait upon You now";

        public ObservableCollection<SongSlide> Albums { get; set; }

        public ProjectorViewModel()
        {

        }
    }
}
