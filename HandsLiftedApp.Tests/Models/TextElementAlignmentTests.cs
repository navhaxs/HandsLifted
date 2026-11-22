using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Layout;
using Avalonia.Threading;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models;

[TestClass]
public class TextElementAlignmentTests
{
    [TestMethod]
    public Task VerticalAlignmentBottom_RaisesItsOwnHelperProperty() => DispatcherTestThread.Run(() =>
    {
        var element = new TextElement();
        Dispatcher.UIThread.RunJobs();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)element).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        element.VerticalAlignment = VerticalAlignment.Bottom;
        Dispatcher.UIThread.RunJobs();

        CollectionAssert.Contains(raised, "CalculatedVerticalAlignmentBottom");
        Assert.IsTrue(element.CalculatedVerticalAlignmentBottom);
        Assert.IsFalse(element.CalculatedVerticalAlignmentCenter);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task VerticalAlignmentChange_DoesNotRaiseHorizontalHelpers() => DispatcherTestThread.Run(() =>
    {
        var element = new TextElement();
        Dispatcher.UIThread.RunJobs();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)element).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        element.VerticalAlignment = VerticalAlignment.Bottom;
        Dispatcher.UIThread.RunJobs();

        CollectionAssert.DoesNotContain(raised, "CalculatedTextAlignmentRight");
        CollectionAssert.DoesNotContain(raised, "CalculatedTextAlignmentLeft");
        CollectionAssert.DoesNotContain(raised, "CalculatedTextAlignmentCenter");
        CollectionAssert.DoesNotContain(raised, "CalculatedTextAlignmentJustify");
        return Task.CompletedTask;
    });
}
