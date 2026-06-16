using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using HandsLiftedApp.Behaviours;
using HandsLiftedApp.Common.Extensions;
using System.Collections.Generic;
using System.Linq;
using HandsLiftedApp.Extensions;

namespace HandsLiftedApp.Controls.Behaviours
{
    /// <summary>
    /// A behavior that allows keyboard navigation between stanza/part editor text boxes.
    /// </summary>
    public sealed class StanzaTextBoxBehavior : Behavior<Control>
    {
        /// <summary>
        /// Identifies the <seealso cref="TargetControl"/> avalonia property.
        /// </summary>
        public static readonly StyledProperty<Control?> TargetControlProperty =
            AvaloniaProperty.Register<DragControlBehavior, Control?>(nameof(TargetControl));

        private Control? _parent;
        private Point _previous;

        /// <summary>
        /// Gets or sets the target control to be moved around instead of <see cref="IBehavior.AssociatedObject"/>. This is a avalonia property.
        /// </summary>
        [ResolveByName]
        public Control? TargetControl
        {
            get => GetValue(TargetControlProperty);
            set => SetValue(TargetControlProperty, value);
        }

        /// <inheritdoc />
        protected override void OnAttachedToVisualTree()
        {
        }

        private int IndexOfStartOfLastLine(string? text)
        {
            if (text == null) return 0;
            int lastNewline = text.LastIndexOf("\n");
            return lastNewline >= 0 ? lastNewline + 1 : 0;
        }

        private List<TextBox> GetTextBoxTree(TextBox activeTextBox)
        {
            UserControl? window = activeTextBox.FindAncestorOfType<UserControl>();
            StackPanel? stackPanel = window.FindControl<StackPanel>("Wrapper");
            var boxes = stackPanel.FindAllVisuals<TextBox>().Distinct().ToList();
            // Copyright lives outside Wrapper; append as last in nav order
            var copyright = window?.FindControl<TextBox>("CopyrightTextBox");
            if (copyright != null && !boxes.Contains(copyright))
                boxes.Add(copyright);
            return boxes;
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject is TextBox textBox)
            {
                textBox.KeyDown += (object? sender, KeyEventArgs e) =>
                {
                    try
                    {
                        var text = textBox.Text ?? string.Empty;
                        int caret = textBox.CaretIndex;

                        if (e.Key == Key.Up)
                        {
                            // on first line if no '\n' exists before the caret
                            bool onFirstLine = text.IndexOf('\n') == -1 || caret <= text.IndexOf('\n');
                            if (onFirstLine)
                            {
                                List<TextBox> boxes = GetTextBoxTree(textBox);
                                int idx = boxes.IndexOf(textBox);
                                if (idx > 0)
                                {
                                    e.Handled = true;
                                    boxes[idx - 1].Focus();
                                    boxes[idx - 1].CaretIndex = IndexOfStartOfLastLine(boxes[idx - 1].Text);
                                }
                            }
                        }
                        else if (e.Key == Key.Down)
                        {
                            // on last line if caret is after the last '\n' (or no '\n')
                            bool onLastLine = text.LastIndexOf('\n') == -1 || caret > text.LastIndexOf('\n');
                            if (onLastLine)
                            {
                                List<TextBox> boxes = GetTextBoxTree(textBox);
                                int idx = boxes.IndexOf(textBox);
                                if (idx + 1 < boxes.Count)
                                {
                                    e.Handled = true;
                                    boxes[idx + 1].Focus();
                                    boxes[idx + 1].CaretIndex = 0;
                                }
                            }
                        }
                    }
                    catch { }
                };
            }
        }

        /// <inheritdoc />
        protected override void OnDetachedFromVisualTree()
        {
        }
    }
}