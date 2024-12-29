using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.ViewModels.Editor.FreeText;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.SlideElement;
using Serilog;

namespace HandsLiftedApp.Core.Views.Editors.FreeText
{
    public partial class FreeTextSlideEditorControl : UserControl
    {
        public FreeTextSlideEditorControl()
        {
            InitializeComponent();
        }

        public void UpdateXml()
        {
            if (this.DataContext is FreeTextSlideEditorViewModel vm)
            {
                _enableXMLParse = false;
                XmlSerializer serializer = new XmlSerializer(typeof(CustomSlide));
                using (StringWriter writer = new())
                {
                    serializer.Serialize(writer, vm.Slide);
                    CodeEditor.Text = writer.ToString();
                }

                _enableXMLParse = true;
            }
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is FreeTextSlideEditorViewModel vm)
            {
                vm.Slide.SlideElements.Add(new TextElement());
            }
        }


        private void MoveItemUp_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is FreeTextSlideEditorViewModel vm)
            {
                if (sender is Control { DataContext: SlideElement slideElement })
                {
                    var indexOf = vm.Slide.SlideElements.IndexOf(slideElement);
                    vm.Slide.SlideElements.Move(indexOf, Math.Max(indexOf - 1, 0));
                }
            }
        }

        private void MoveItemDown_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is FreeTextSlideEditorViewModel vm)
            {
                if (sender is Control { DataContext: SlideElement slideElement })
                {
                    var indexOf = vm.Slide.SlideElements.IndexOf(slideElement);
                    vm.Slide.SlideElements.Move(indexOf, Math.Min(indexOf + 1, vm.Slide.SlideElements.Count - 1));
                }
            }
        }

        private void DeleteItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is FreeTextSlideEditorViewModel vm)
            {
                if (sender is Control { DataContext: SlideElement slideElement })
                {
                    vm.Slide.SlideElements.Remove(slideElement);
                }
            }
        }

        private void UpdateXmlButton_OnClick(object? sender, RoutedEventArgs e)
        {
            UpdateXml();
        }

        private bool _enableXMLParse = true;

        private void CodeEditor_OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_enableXMLParse && this.DataContext is FreeTextSlideEditorViewModel vm)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(CustomSlide));

                    using (StringReader reader = new StringReader(CodeEditor.Text))
                    {
                        vm.Slide = serializer.Deserialize(reader) as CustomSlide;
                    }
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Failed to parse XML");
                }
            }
        }
    }
}