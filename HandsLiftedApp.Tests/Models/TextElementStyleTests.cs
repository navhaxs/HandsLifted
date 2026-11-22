using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia.Media;
using Avalonia.Threading;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models;

[TestClass]
public class TextElementStyleTests
{
    private static string Serialize(CustomSlide slide)
    {
        var serializer = new XmlSerializer(typeof(CustomSlide));
        using var writer = new StringWriter();
        serializer.Serialize(writer, slide);
        return writer.ToString();
    }

    private static CustomSlide Deserialize(string xml)
    {
        var serializer = new XmlSerializer(typeof(CustomSlide));
        using var reader = new StringReader(xml);
        return (CustomSlide)serializer.Deserialize(reader)!;
    }

    [TestMethod]
    public Task Xml_RoundTrips_ItalicAndUnderline() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        slide.SlideElements.Add(new TextElement { Text = "Hi", Italic = true, Underline = true });

        var loaded = Deserialize(Serialize(slide));

        var t = (TextElement)loaded.SlideElements.Single();
        Assert.IsTrue(t.Italic);
        Assert.IsTrue(t.Underline);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Xml_WithoutItalicUnderline_LoadsAsFalse() => DispatcherTestThread.Run(() =>
    {
        var slide = new CustomSlide();
        slide.SlideElements.Add(new TextElement { Text = "Hi", Italic = true, Underline = true });
        var xml = Serialize(slide);
        xml = Regex.Replace(xml, @"<(\w+:)?(Italic|Underline)>[^<]*</(\w+:)?(Italic|Underline)>", "");
        Assert.IsFalse(xml.Contains("Italic") || xml.Contains("Underline"), "elements stripped");

        var loaded = Deserialize(xml);

        var t = (TextElement)loaded.SlideElements.Single();
        Assert.IsFalse(t.Italic);
        Assert.IsFalse(t.Underline);
        Assert.AreEqual("Hi", t.Text);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task IsBold_Setter_TogglesFontWeight_AndRaisesNotification() => DispatcherTestThread.Run(() =>
    {
        var element = new TextElement();
        Dispatcher.UIThread.RunJobs();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)element).PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        Assert.IsFalse(element.IsBold);

        element.IsBold = true;
        Assert.AreEqual(FontWeight.Bold, element.FontWeight);
        Assert.IsTrue(element.IsBold);
        CollectionAssert.Contains(raised, "IsBold");

        raised.Clear();
        element.FontWeight = FontWeight.Normal; // e.g. undo writing FontWeight directly
        Assert.IsFalse(element.IsBold);
        CollectionAssert.Contains(raised, "IsBold");

        element.IsBold = true;
        element.IsBold = false;
        Assert.AreEqual(FontWeight.Normal, element.FontWeight);
        return Task.CompletedTask;
    });
}
