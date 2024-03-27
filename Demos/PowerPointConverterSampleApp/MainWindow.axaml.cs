﻿using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Importer.PowerPointInteropData;
using HandsLiftedApp.Importer.PowerPointLib;
using ProtoBuf;

namespace PowerPointConverterSampleApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        new Thread(() => 
        {
            while (true)
            {
                using (var server = new NamedPipeServerStream("MyPipeResult"))
                {
                    Console.WriteLine("Waiting for connection...");
                    server.WaitForConnection();

                    var x = Serializer.Deserialize<ImportResult>(server);
                    System.Diagnostics.Debug.Print(x.ToString());
                }
            }
        }).Start();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        //Class1.Run(InputPPTX.Text);
        
        using (var client = new NamedPipeClientStream(".", "MyPipe", PipeDirection.Out))
        {
            client.Connect();

            ImportTask importTask = new ImportTask() { PPTXFilePath = InputPPTX.Text };
            Serializer.Serialize(client, importTask);
        }
    }
}