using System;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using Serilog;

namespace HandsLiftedApp.Core.Render
{
    public partial class CustomAxamlSlideRender : UserControl
    {
        public CustomAxamlSlideRender()
        {
            InitializeComponent();

            TryLoadAxamlContent();
            this.DataContextChanged += (sender, args) => TryLoadAxamlContent();
        }

        void TryLoadAxamlContent()
        {
            if (DataContext is CustomAxamlSlideInstance dataContext)
            try
            {
                string xaml;
                if (dataContext.SourceMediaFilePath != null)
                {
                    using (StreamReader streamReader = new StreamReader(dataContext.SourceMediaFilePath, Encoding.UTF8))
                    {
                        xaml = streamReader.ReadToEnd();
                    }
                }
                else
                {
                    xaml =
                        @"<ContentControl xmlns='https://github.com/avaloniaui' Background='#006eff' Content='My XAML loaded data'/>";
                }

                var target = AvaloniaRuntimeXamlLoader.Parse<ContentControl>(xaml);
                this.Content = target;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load AXAML: {XamlFilePath}", ex, dataContext.SourceMediaFilePath);
            }
        }
    }
}