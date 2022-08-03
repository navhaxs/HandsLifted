﻿using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("Item", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public abstract class Item<T> : ReactiveObject where T : IItemState
    {
        [XmlIgnore]
        public Guid Uuid { get; set; }

        private T _state;
        [XmlIgnore]
        public T State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }

        public string _title = "New Item";

        protected Item()
        {
            State = (T)Activator.CreateInstance(typeof(T), this);
        }

        public string Title
        {
            get => _title; set
            {
                this.RaiseAndSetIfChanged(ref _title, value);
                this.RaisePropertyChanged(nameof(Slides));
            }
        }

        [XmlIgnore]
        public abstract ObservableCollection<Slide> Slides { get; }
    }

    public interface IItemState
    {
        //public int ItemIndex { get; set; }
        public int SelectedIndex { get; set; }
    }
}
