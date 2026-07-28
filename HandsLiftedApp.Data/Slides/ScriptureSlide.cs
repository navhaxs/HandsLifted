using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Data.Slides
{
    public class ScriptureSlide : Slide
    {
        public string Id { get; set; }

        public ScriptureSlide(ScriptureItem? parentScriptureItem, string id)
        {
            ParentScriptureItem = parentScriptureItem;
            Id = id;
        }

        private string _text = "";
        public string Text
        {
            get => _text;
            set => this.RaiseAndSetIfChanged(ref _text, value);
        }

        private string _label = "";
        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        public override string? SlideText => Text;

        public override string? SlideLabel => Label;

        public ScriptureItem? ParentScriptureItem { get; } = null;

        public override bool Equals(Object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                ScriptureSlide p = (ScriptureSlide)obj;
                return (Id == p.Id);
            }
        }
    }
}
