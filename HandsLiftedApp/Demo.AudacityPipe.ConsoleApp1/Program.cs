// See https://aka.ms/new-console-template for more information
using System.IO.Pipes;
using System.Text;

Console.WriteLine("Hello, World!");

using (NamedPipeClientStream pipeTo =
           new NamedPipeClientStream(".", "ToSrvPipe", PipeDirection.Out))
using (NamedPipeClientStream pipeFrom =
        new NamedPipeClientStream(".", "FromSrvPipe", PipeDirection.In))
{

    // Connect to the pipe or wait until the pipe is available.
    Console.Write("Attempting to connect to pipe...");
    pipeTo.Connect();
    pipeFrom.Connect();

    Console.WriteLine("Connected to pipe.");
    Console.WriteLine("There are currently {0} pipe server instances open.",
       pipeTo.NumberOfServerInstances);
    Console.WriteLine("There are currently {0} pipe server instances open.",
       pipeFrom.NumberOfServerInstances);


    Thread.Sleep(4000);
    pipeTo.Write(Encoding.ASCII.GetBytes("Help: Command=Help"));
    pipeTo.Write(Encoding.ASCII.GetBytes("Record1stChoice:"));


    using (StreamReader sr = new StreamReader(pipeFrom))
    {
        // Display the read text to the console
        string temp;
        while ((temp = sr.ReadLine()) != null)
        {
            Console.WriteLine("Received from server: {0}", temp);
        }
    }
    //pipeClient.Flush();
}
Console.Write("Press Enter to continue...");
Console.ReadLine();
