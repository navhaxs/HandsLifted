using System;
using System.IO;
using System.Xml.Serialization;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;

namespace HandsLiftedApp.Core.Views.Editors;

/// <summary>
/// Element actions of the custom slide editor, kept free of UI so they can be tested.
/// Index 0 of <see cref="CustomSlide.SlideElements"/> is the frontmost element.
/// </summary>
public static class SlideElementCommands
{
    private const int DuplicateOffset = 20;

    /// <summary>Copies the element (XML round trip, so every style comes along), offsets it and puts it directly in front of the source.</summary>
    public static SlideElement? Duplicate(CustomSlide slide, SlideElement source)
    {
        var index = slide.SlideElements.IndexOf(source);
        if (index < 0) return null;

        var serializer = new XmlSerializer(typeof(SlideElement));
        SlideElement? copy;
        using (var writer = new StringWriter())
        {
            serializer.Serialize(writer, source);
            using var reader = new StringReader(writer.ToString());
            copy = serializer.Deserialize(reader) as SlideElement;
        }
        if (copy == null) return null;

        copy.X = Math.Max(0, Math.Min(source.X + DuplicateOffset, slide.SlideWidth - copy.Width));
        copy.Y = Math.Max(0, Math.Min(source.Y + DuplicateOffset, slide.SlideHeight - copy.Height));
        slide.SlideElements.Insert(index, copy);
        return copy;
    }

    public static bool Delete(CustomSlide slide, SlideElement? element)
        => element != null && slide.SlideElements.Remove(element);

    /// <summary>Moves the element one step toward the front (index 0).</summary>
    public static bool BringForward(CustomSlide slide, SlideElement element)
    {
        var index = slide.SlideElements.IndexOf(element);
        if (index <= 0) return false;
        slide.SlideElements.Move(index, index - 1);
        return true;
    }

    /// <summary>Moves the element one step toward the back (end of the list).</summary>
    public static bool SendBackward(CustomSlide slide, SlideElement element)
    {
        var index = slide.SlideElements.IndexOf(element);
        if (index < 0 || index >= slide.SlideElements.Count - 1) return false;
        slide.SlideElements.Move(index, index + 1);
        return true;
    }

    /// <summary>Moves the element in front of everything else (index 0).</summary>
    public static bool BringToFront(CustomSlide slide, SlideElement element)
    {
        var index = slide.SlideElements.IndexOf(element);
        if (index <= 0) return false;
        slide.SlideElements.Move(index, 0);
        return true;
    }

    /// <summary>Moves the element behind everything else (end of the list).</summary>
    public static bool SendToBack(CustomSlide slide, SlideElement element)
    {
        var index = slide.SlideElements.IndexOf(element);
        if (index < 0 || index >= slide.SlideElements.Count - 1) return false;
        slide.SlideElements.Move(index, slide.SlideElements.Count - 1);
        return true;
    }

    public static bool Nudge(SlideElement? element, int dx, int dy)
    {
        if (element == null) return false;
        element.X += dx;
        element.Y += dy;
        return true;
    }

    /// <summary>
    /// Starting box for a new text element. The model's own 100x100 default only fits "Te" at the default 90pt
    /// font size, so a freshly added box would look broken.
    /// </summary>
    public const int DefaultTextWidth = 1200;
    public const int DefaultTextHeight = 300;

    public static TextElement AddText(CustomSlide slide)
    {
        var element = new TextElement { Text = "Text", Width = DefaultTextWidth, Height = DefaultTextHeight };
        AddCentred(slide, element);
        return element;
    }

    public static ImageElement AddImage(CustomSlide slide)
    {
        var element = new ImageElement();
        AddCentred(slide, element);
        return element;
    }

    private static void AddCentred(CustomSlide slide, SlideElement element)
    {
        element.X = slide.SlideWidth / 2 - element.Width / 2;
        element.Y = slide.SlideHeight / 2 - element.Height / 2;
        slide.SlideElements.Insert(0, element);
    }
}
