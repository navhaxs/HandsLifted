using System;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Data.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.ItemEditDock
{
    public partial class ItemTimer : UserControl
    {
        private IDisposable? _subscription;

        public ItemTimer()
        {
            InitializeComponent();

            DataContextChanged += (sender, args) =>
            {
                _subscription?.Dispose();
                
                if (DataContext is ItemAutoAdvanceTimer itemAutoAdvanceTimer)
                {
                    _subscription = itemAutoAdvanceTimer
                        .WhenAnyValue(x => x.IsEnabled)
                        .Subscribe(x =>
                        {
                            MessageBus.Current.SendMessage(new OnTimerEnabledToggleEvent());
                        });
                }
            };
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _subscription?.Dispose();
            base.OnDetachedFromVisualTree(e);
        }
    }
}