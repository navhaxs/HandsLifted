using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.ViewModels
{
    public class StageDisplayViewModel : ReactiveObject
    {
        private int _SelectedIndex = 1;
        public int SelectedIndex
        {
            get => _SelectedIndex;
            set => this.RaiseAndSetIfChanged(ref _SelectedIndex, value);
        }
    }
}
