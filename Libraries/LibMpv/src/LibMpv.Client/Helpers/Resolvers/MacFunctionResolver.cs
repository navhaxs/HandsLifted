using System.Runtime.InteropServices;

namespace LibMpv.Client.Native;

public class MacFunctionResolver : FunctionResolverBase
{
    private const string Libdl = "libdl";

    private const int RTLD_NOW = 0x002;

    protected override string GetNativeLibraryName(string libraryName, int version)
    {
        if (version > 0) return $"{libraryName}.{version}.dylib";
        return $"{libraryName}.dylib";
    }
    
    protected override IntPtr LoadNativeLibrary(string libraryPath)
    {
        Console.WriteLine($"[LoadNativeLibrary] Attempting to load: {libraryPath}");
        
        // Clear any previous error
        _ = dlerror();

        var handle = dlopen(libraryPath, RTLD_NOW);
        var error = GetDlError();
        
        if (handle == IntPtr.Zero)
        {
            Console.WriteLine($"[LoadNativeLibrary] Failed to load '{libraryPath}': {error}");
        }
        else
        {
            Console.WriteLine($"[LoadNativeLibrary] Successfully loaded: {libraryPath}");
        }

        return handle;
    }
    
    protected override IntPtr FindFunctionPointer(IntPtr nativeLibraryHandle, string functionName)
    {
        var symbol = dlsym(nativeLibraryHandle, functionName);
        if (symbol == IntPtr.Zero)
        {
            var error = GetDlError();
            Console.WriteLine($"[FindFunctionPointer] Failed to resolve symbol '{functionName}': {error}");
        }
        else
        {
            Console.WriteLine($"[FindFunctionPointer] Resolved symbol '{functionName}'");
        }

        return symbol;
    }

    [DllImport(Libdl)]
    public static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport(Libdl)]
    public static extern IntPtr dlopen(string fileName, int flag);
    
    [DllImport(Libdl)]
    private static extern IntPtr dlerror();

    private static string GetDlError()
    {
        var errPtr = dlerror();
        return errPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errPtr) : null;
    }

}
