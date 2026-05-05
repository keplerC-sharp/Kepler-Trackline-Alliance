using System.Runtime.InteropServices;

namespace KeplerPrint.Helpers
{
    /// <summary>
    /// Wrapper for the Windows WinSpool API to allow raw byte dispatching to 
    /// local thermal printers. Essential for ESC/POS command execution.
    /// </summary>
    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string  pDocName   = "KeplerTicket";
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string  pDataType  = "RAW";
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA",
            SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool OpenPrinter(string szPrinter,
            out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter",
            SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA",
            SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level,
            [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter",
            SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter",
            SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter",
            SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter",
            SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes,
            int dwCount, out int dwWritten);

        /// <summary>
        /// Sends a raw byte array directly to the specified printer spooler.
        /// Handles low-level memory allocation and Win32 interop sequences.
        /// </summary>
        public static void SendBytesToPrinter(string printerName, byte[] bytes)
        {
            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
                throw new InvalidOperationException(
                    $"Unable to open printer connection for '{printerName}'. " +
                    $"Win32Error Code: {Marshal.GetLastWin32Error()}");
            try
            {
                var di = new DOCINFOA();
                if (!StartDocPrinter(hPrinter, 1, di))
                    throw new InvalidOperationException(
                        $"WinSpool StartDocPrinter sequence failed. Win32Error Code: {Marshal.GetLastWin32Error()}");

                StartPagePrinter(hPrinter);

                var pBytes = Marshal.AllocCoTaskMem(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, pBytes, bytes.Length);
                    WritePrinter(hPrinter, pBytes, bytes.Length, out _);
                }
                finally { Marshal.FreeCoTaskMem(pBytes); }

                EndPagePrinter(hPrinter);
                EndDocPrinter(hPrinter);
            }
            finally { ClosePrinter(hPrinter); }
        }
    }
}
