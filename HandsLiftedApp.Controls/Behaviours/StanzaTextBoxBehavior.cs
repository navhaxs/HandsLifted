using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using HandsLiftedApp.Behaviours;
using HandsLiftedApp.Common.Extensions;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

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

        int lastCaretIndex;
        int caretIndex;
        int lastSeenCaretIndex;
        private int IndexOfStartOfLastLine(string? text)
        {
            if (text == null)
            {
                return 0;
            }
            int lastNewline = text.LastIndexOf("\n");
            if (lastNewline > 0)
            {
                return lastNewline + 1;
            }
            else
            {
                return 0;
            }
        }

        private List<TextBox> GetTextBoxTree(TextBox activeTextBox)
        {
            Window? window = activeTextBox.GetVisualRoot() as Window;
            StackPanel? stackPanel = window.FindControl<StackPanel>("Wrapper");
            return stackPanel.FindAllVisuals<TextBox>().Distinct().ToList();
        }

        /* NOTES: This does not work for text wrapping */
        protected override void OnAttached()
        {
            base.OnAttached();

            var source = AssociatedObject;
            if (source is { })
            {
                if (source is TextBox textBox)
                {
                    textBox.WhenAnyValue(textBox => textBox.CaretIndex).Subscribe(idx =>
                    {
                        lastCaretIndex = caretIndex;
                        caretIndex = idx;
                    });

                    textBox.KeyDown += (object? sender, KeyEventArgs e) =>
                    {
                        try
                        {
                            if (e.Key == Key.Up)
                            {
                                var text = textBox.Text;
                                int firstNewline = text.IndexOf("\n");

                                if (lastCaretIndex <= firstNewline || caretIndex <= firstNewline && lastSeenCaretIndex == textBox.CaretIndex || firstNewline == -1)
                                {
                                    List<TextBox> enumerable = GetTextBoxTree(textBox);
                                    int idx = enumerable.IndexOf(textBox);
                                    if (idx > 0)
                                    {
                                        enumerable[idx - 1].Focus();
                                        enumerable[idx - 1].CaretIndex = IndexOfStartOfLastLine(enumerable[idx - 1].Text);
                                    }
                                }
                            }
                            else if (e.Key == Key.Down)
                            {
                                var text = textBox.Text;
                                int lastNewline = text.LastIndexOf("\n");

                                if (lastCaretIndex > lastNewline || caretIndex > lastCaretIndex && lastSeenCaretIndex == textBox.CaretIndex || lastNewline == -1)
                                {
                                    List<TextBox> enumerable = GetTextBoxTree(textBox);
                                    int idx = enumerable.IndexOf(textBox);
                                    if (idx + 1 < enumerable.Count)
                                    {
                                        enumerable[idx + 1].Focus();
                                        enumerable[idx + 1].CaretIndex = 0;
                                    }
                                }
                            }
                        }
                        catch { }
                        lastSeenCaretIndex = textBox.CaretIndex;
                    };
                }
            }
        }

        /// <inheritdoc />
        protected override void OnDetachedFromVisualTree()
        {
        }
    }
}