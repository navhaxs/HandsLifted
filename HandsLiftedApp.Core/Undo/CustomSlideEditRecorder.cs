using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// Undo history for editing one custom slide (the custom slide editor: element list, visual drag/resize, the
    /// formatting boxes, colour pickers, background).
    ///
    /// Same approach as <see cref="SongEditRecorder"/>: watch the slide for any change, compare a snapshot of its
    /// whole editable state against the previous one, and record a difference as one before/after step. Drags and
    /// typing merge into a single step while the pauses are short. Undo writes the snapshot back in place.
    ///
    /// Elements have no identity of their own, so undo writes element N's saved values into the element currently at
    /// position N (replacing it only when the kind differs, or adding/removing at the end). The slide looks
    /// identical afterwards; the editor's current element selection may reset.
    /// </summary>
    public sealed class CustomSlideEditRecorder : IDisposable
    {
        private readonly UndoStack _stack;
        private readonly CustomSlide _slide;
        private readonly int _ownerThread = Environment.CurrentManagedThreadId;
        private readonly List<SlideElement> _hooked = new();
        private ObservableCollection<SlideElement>? _elements;
        private SlideSnapshot _last;

        public CustomSlideEditRecorder(UndoStack stack, CustomSlide slide)
        {
            _stack = stack;
            _slide = slide;
            _last = SlideSnapshot.Of(slide);

            slide.PropertyChanged += OnSlideChanged;
            HookElements();
        }

        public void Dispose()
        {
            _slide.PropertyChanged -= OnSlideChanged;
            UnhookElements();
        }

        private void HookElements()
        {
            UnhookElements();
            _elements = _slide.SlideElements;
            _elements.CollectionChanged += OnElementsChanged;
            foreach (var e in _elements) HookElement(e);
        }

        private void UnhookElements()
        {
            if (_elements != null) _elements.CollectionChanged -= OnElementsChanged;
            foreach (var e in _hooked.ToList()) e.PropertyChanged -= OnElementChanged;
            _hooked.Clear();
            _elements = null;
        }

        private void HookElement(SlideElement e)
        {
            if (_hooked.Contains(e)) return;
            e.PropertyChanged += OnElementChanged;
            _hooked.Add(e);
        }

        private void OnSlideChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CustomSlide.SlideElements)) HookElements();
            OnChanged();
        }

        private void OnElementsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_elements != null)
            {
                foreach (var gone in _hooked.Where(h => !_elements.Contains(h)).ToList())
                {
                    gone.PropertyChanged -= OnElementChanged;
                    _hooked.Remove(gone);
                }

                foreach (var el in _elements) HookElement(el);
            }

            OnChanged();
        }

        private void OnElementChanged(object? sender, PropertyChangedEventArgs e) => OnChanged();

        private int _batchDepth;
        private string? _batchDescription;

        /// <summary>Everything changed inside the returned scope becomes ONE step with the given description.</summary>
        public IDisposable BeginBatch(string description)
        {
            if (_batchDepth == 0) _batchDescription = description;
            _batchDepth++;
            return new BatchScope(() =>
            {
                if (--_batchDepth > 0) return;
                var d = _batchDescription;
                _batchDescription = null;
                Flush(d);
            });
        }

        private sealed class BatchScope : IDisposable
        {
            private Action? _onDispose;
            public BatchScope(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }

        private void OnChanged()
        {
            if (_stack.IsApplying || _batchDepth > 0) return;
            Flush(null);
        }

        private void Flush(string? forcedDescription)
        {
            var now = SlideSnapshot.Of(_slide);
            var old = _last;
            _last = now;
            if (old.Equals(now)) return;
            if (Environment.CurrentManagedThreadId != _ownerThread) return;

            _stack.Record(new SlideStateAction(old, now, forcedDescription ?? SlideSnapshot.Describe(old, now), Write));
        }

        private void Write(SlideSnapshot target)
        {
            if (_slide.BackgroundAvaloniaColour != target.Background) _slide.BackgroundAvaloniaColour = target.Background;
            if (_slide.BackgroundGraphicFilePath != target.BackgroundGraphic)
                _slide.BackgroundGraphicFilePath = target.BackgroundGraphic;

            var elements = _slide.SlideElements;

            // same kind at the same position: update in place; otherwise replace; then trim or extend
            for (var i = 0; i < target.Elements.Count; i++)
            {
                var t = target.Elements[i];
                if (i < elements.Count && t.AppliesTo(elements[i]))
                {
                    t.CopyTo(elements[i]);
                }
                else if (i < elements.Count)
                {
                    elements[i] = t.Create();
                }
                else
                {
                    elements.Add(t.Create());
                }
            }

            while (elements.Count > target.Elements.Count) elements.RemoveAt(elements.Count - 1);

            _last = SlideSnapshot.Of(_slide);
        }
    }

    internal sealed class SlideStateAction : IUndoableAction
    {
        // continuous edits (drags, typing, colour pickers) merge into the previous step when recorded soon after it
        private static readonly HashSet<string> Mergeable = new()
        {
            "Move Element", "Resize Element", "Edit Text", "Format Text", "Change Colour", "Change Background"
        };

        private readonly SlideSnapshot _before;
        private SlideSnapshot _after;
        private readonly Action<SlideSnapshot> _write;

        public SlideStateAction(SlideSnapshot before, SlideSnapshot after, string description, Action<SlideSnapshot> write)
        {
            _before = before;
            _after = after;
            Description = description;
            _write = write;
        }

        public string Description { get; private set; }

        public void Do() => _write(_after);
        public void Undo() => _write(_before);

        private static bool IsGeometry(string d) => d is "Move Element" or "Resize Element";

        public bool TryMerge(IUndoableAction next)
        {
            if (next is not SlideStateAction n) return false;

            // A resize writes X, Y, Width and Height one property at a time, so one drag alternates between
            // "move" and "resize" snapshots: geometry edits merge with each other, and the step is a resize if
            // any part of it was.
            var geometry = IsGeometry(Description) && IsGeometry(n.Description);
            var sameKind = n.Description == Description && Mergeable.Contains(Description);
            if (!geometry && !sameKind) return false;

            _after = n._after;
            if (geometry && (Description == "Resize Element" || n.Description == "Resize Element"))
                Description = "Resize Element";
            return true;
        }
    }

    /// <summary>The editable state of a custom slide, with value equality.</summary>
    internal sealed class SlideSnapshot : IEquatable<SlideSnapshot>
    {
        public Color Background { get; }
        public string? BackgroundGraphic { get; }
        public IReadOnlyList<ElementState> Elements { get; }
        private readonly string _key;

        private SlideSnapshot(Color background, string? graphic, IReadOnlyList<ElementState> elements)
        {
            Background = background;
            BackgroundGraphic = graphic;
            Elements = elements;
            _key = $"{background}|{graphic ?? "~"}|" + string.Join("\u0002", elements.Select(e => e.Key));
        }

        public static SlideSnapshot Of(CustomSlide slide) => new(
            slide.BackgroundAvaloniaColour,
            slide.BackgroundGraphicFilePath,
            slide.SlideElements.Select(ElementState.Of).ToList());

        public bool Equals(SlideSnapshot? other) => other != null && _key == other._key;
        public override bool Equals(object? obj) => Equals(obj as SlideSnapshot);
        public override int GetHashCode() => _key.GetHashCode();

        public static string Describe(SlideSnapshot a, SlideSnapshot b)
        {
            var kinds = new List<string>();
            if (a.Background != b.Background || a.BackgroundGraphic != b.BackgroundGraphic) kinds.Add("Change Background");

            if (a.Elements.Count < b.Elements.Count) kinds.Add("Add Element");
            else if (a.Elements.Count > b.Elements.Count) kinds.Add("Delete Element");
            else if (!a.Elements.Select(e => e.Key).SequenceEqual(b.Elements.Select(e => e.Key)))
            {
                var sameSet = a.Elements.Select(e => e.Key).OrderBy(k => k, StringComparer.Ordinal)
                    .SequenceEqual(b.Elements.Select(e => e.Key).OrderBy(k => k, StringComparer.Ordinal));
                if (sameSet)
                {
                    kinds.Add("Reorder Elements");
                }
                else
                {
                    for (var i = 0; i < a.Elements.Count; i++)
                        if (a.Elements[i].Key != b.Elements[i].Key)
                            kinds.Add(ElementState.Describe(a.Elements[i], b.Elements[i]));
                }
            }

            var distinct = kinds.Distinct().ToList();
            return distinct.Count == 1 ? distinct[0] : "Edit Slide";
        }
    }

    /// <summary>One element's saved values.</summary>
    internal abstract class ElementState
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }

        public abstract string Key { get; }
        public abstract bool AppliesTo(SlideElement element);
        public abstract SlideElement Create();

        public virtual void CopyTo(SlideElement element)
        {
            if (element.X != X) element.X = X;
            if (element.Y != Y) element.Y = Y;
            if (element.Width != Width) element.Width = Width;
            if (element.Height != Height) element.Height = Height;
        }

        public static ElementState Of(SlideElement element) => element switch
        {
            TextElement t => new TextState
            {
                X = t.X, Y = t.Y, Width = t.Width, Height = t.Height, Text = t.Text, FontFamily = t.FontFamilyAsText,
                FontWeight = t.FontWeight, FontSize = t.FontSize, TextAlignment = t.TextAlignment,
                VerticalAlignment = t.VerticalAlignment, Foreground = t.ForegroundAvaloniaColour,
                Background = t.BackgroundAvaloniaColour, LineHeightEm = t.LineHeightEm
            },
            ImageElement i => new ImageState { X = i.X, Y = i.Y, Width = i.Width, Height = i.Height, FilePath = i.FilePath },
            _ => throw new NotSupportedException($"Unknown slide element type {element.GetType().Name}")
        };

        public static string Describe(ElementState a, ElementState b)
        {
            if (a.GetType() != b.GetType()) return "Edit Slide";
            var kinds = new List<string>();
            if (a.X != b.X || a.Y != b.Y) kinds.Add("Move Element");
            if (a.Width != b.Width || a.Height != b.Height) kinds.Add("Resize Element");

            if (a is TextState ta && b is TextState tb)
            {
                if (ta.Text != tb.Text) kinds.Add("Edit Text");
                if (ta.Foreground != tb.Foreground || ta.Background != tb.Background) kinds.Add("Change Colour");
                if (ta.FontFamily != tb.FontFamily || ta.FontWeight != tb.FontWeight || ta.FontSize != tb.FontSize
                    || ta.TextAlignment != tb.TextAlignment || ta.VerticalAlignment != tb.VerticalAlignment
                    || ta.LineHeightEm != tb.LineHeightEm)
                    kinds.Add("Format Text");
            }
            else if (a is ImageState ia && b is ImageState ib && ia.FilePath != ib.FilePath)
            {
                kinds.Add("Change Image");
            }

            // Dragging a top/left handle (or a corner) moves the origin as well as changing the size. That is still
            // one gesture - a resize - so it must keep one name or its steps would not merge into one.
            if (kinds.Count > 0 && kinds.All(k => k is "Move Element" or "Resize Element"))
                return kinds.Contains("Resize Element") ? "Resize Element" : "Move Element";

            return kinds.Count == 1 ? kinds[0] : "Edit Slide";
        }
    }

    internal sealed class TextState : ElementState
    {
        public string Text { get; init; } = "";
        public string FontFamily { get; init; } = "";
        public FontWeight FontWeight { get; init; }
        public int FontSize { get; init; }
        public Avalonia.Media.TextAlignment TextAlignment { get; init; }
        public Avalonia.Layout.VerticalAlignment VerticalAlignment { get; init; }
        public Color Foreground { get; init; }
        public Color Background { get; init; }
        public decimal LineHeightEm { get; init; }

        public override string Key =>
            $"T|{X}|{Y}|{Width}|{Height}|{Text.Length}:{Text}|{FontFamily}|{(int)FontWeight}|{FontSize}|{TextAlignment}|{VerticalAlignment}|{Foreground}|{Background}|{LineHeightEm}";

        public override bool AppliesTo(SlideElement element) => element is TextElement;
        public override SlideElement Create()
        {
            var t = new TextElement();
            CopyTo(t);
            return t;
        }

        public override void CopyTo(SlideElement element)
        {
            base.CopyTo(element);
            var t = (TextElement)element;
            if (t.Text != Text) t.Text = Text;
            if (t.FontFamilyAsText != FontFamily) t.FontFamilyAsText = FontFamily;
            if (t.FontWeight != FontWeight) t.FontWeight = FontWeight;
            if (t.FontSize != FontSize) t.FontSize = FontSize;
            if (t.TextAlignment != TextAlignment) t.TextAlignment = TextAlignment;
            if (t.VerticalAlignment != VerticalAlignment) t.VerticalAlignment = VerticalAlignment;
            if (t.ForegroundAvaloniaColour != Foreground) t.ForegroundAvaloniaColour = Foreground;
            if (t.BackgroundAvaloniaColour != Background) t.BackgroundAvaloniaColour = Background;
            if (t.LineHeightEm != LineHeightEm) t.LineHeightEm = LineHeightEm;
        }
    }

    internal sealed class ImageState : ElementState
    {
        public string FilePath { get; init; } = "";

        public override string Key => $"I|{X}|{Y}|{Width}|{Height}|{FilePath.Length}:{FilePath}";
        public override bool AppliesTo(SlideElement element) => element is ImageElement;
        public override SlideElement Create()
        {
            var i = new ImageElement();
            CopyTo(i);
            return i;
        }

        public override void CopyTo(SlideElement element)
        {
            base.CopyTo(element);
            var i = (ImageElement)element;
            if (i.FilePath != FilePath) i.FilePath = FilePath;
        }
    }
}
