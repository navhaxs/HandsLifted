using Avalonia;
using System;
using System.Collections.ObjectModel;

namespace ScratchApp
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();

        public static void Test()
        {
            ObservableCollection<MyBase<MyInterface>> a = new ObservableCollection<MyBase<MyInterface>>();
            a.Add(new MyBase<MyInterface>());
            a.Add(new SubClass<MyInterface>());
            a.Add(new AnotherSubClass<MyInterface, ExtraInterface>());
        }
    }

    class MyBase<T> where T : MyInterface
    {

    }

    interface MyInterface
    {
    }

    class SubClass<T> : MyBase<T> where T : MyInterface
    {

    }

    interface SubInterface : MyInterface
    {
    }

    class AnotherSubClass<T, X> : MyBase<T> where T : MyInterface where X : ExtraInterface
    {

    }

    interface ExtraInterface
    {
    }



}
