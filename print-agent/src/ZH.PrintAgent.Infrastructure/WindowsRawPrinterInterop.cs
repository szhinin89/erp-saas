using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ZH.PrintAgent.Infrastructure;

internal static class WindowsRawPrinterInterop
{
    private const int PrinterEnumLocal = 0x00000002;
    private const int PrinterEnumConnections = 0x00000004;

    public static IReadOnlyList<string> EnumeratePrinterNames()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Array.Empty<string>();
        }

        const int flags = PrinterEnumLocal | PrinterEnumConnections;
        EnumPrinters(flags, null, 4, IntPtr.Zero, 0, out var neededBytes, out _);
        if (neededBytes <= 0)
        {
            return Array.Empty<string>();
        }

        var buffer = Marshal.AllocHGlobal(neededBytes);
        try
        {
            if (!EnumPrinters(flags, null, 4, buffer, neededBytes, out _, out var returned))
            {
                throw CreateWin32Exception("Unable to enumerate Windows printers.");
            }

            var structSize = Marshal.SizeOf<PrinterInfo4>();
            var names = new List<string>(returned);
            for (var i = 0; i < returned; i++)
            {
                var current = Marshal.PtrToStructure<PrinterInfo4>(buffer + (i * structSize));
                if (!string.IsNullOrWhiteSpace(current.PrinterName))
                {
                    names.Add(current.PrinterName);
                }
            }

            return names;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static string? GetDefaultPrinterName()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        var buffer = new StringBuilder(512);
        var size = buffer.Capacity;
        return GetDefaultPrinter(buffer, ref size) ? buffer.ToString() : null;
    }

    public static bool CanOpenPrinter(string printerName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
        {
            return false;
        }

        ClosePrinter(handle);
        return true;
    }

    public static void WriteRaw(string printerName, string documentName, byte[] bytes)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows raw printing requires Windows.");
        }

        if (!OpenPrinter(printerName, out var printerHandle, IntPtr.Zero))
        {
            throw CreateWin32Exception($"Printer '{printerName}' could not be opened.");
        }

        try
        {
            var documentInfo = new DocumentInfo
            {
                DocumentName = documentName,
                OutputFile = null,
                DataType = "RAW"
            };

            if (StartDocPrinter(printerHandle, 1, ref documentInfo) == 0)
            {
                throw CreateWin32Exception($"Raw print document could not be started for printer '{printerName}'.");
            }

            try
            {
                if (!StartPagePrinter(printerHandle))
                {
                    throw CreateWin32Exception($"Raw print page could not be started for printer '{printerName}'.");
                }

                try
                {
                    if (!WritePrinter(printerHandle, bytes, bytes.Length, out var written) || written != bytes.Length)
                    {
                        throw CreateWin32Exception($"Raw print data could not be written to printer '{printerName}'.");
                    }
                }
                finally
                {
                    EndPagePrinter(printerHandle);
                }
            }
            finally
            {
                EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }

    private static Win32Exception CreateWin32Exception(string message)
    {
        return new Win32Exception(Marshal.GetLastWin32Error(), message);
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printerHandle, IntPtr printerDefaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr printerHandle, int level, ref DocumentInfo documentInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr printerHandle, byte[] bytes, int byteCount, out int bytesWritten);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumPrinters(
        int flags,
        string? name,
        int level,
        IntPtr printerEnum,
        int bufferSize,
        out int bytesNeeded,
        out int countReturned);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDefaultPrinter(StringBuilder buffer, ref int bufferSize);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocumentInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string DocumentName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? OutputFile;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string DataType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrinterInfo4
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? PrinterName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ServerName;

        public uint Attributes;
    }
}
