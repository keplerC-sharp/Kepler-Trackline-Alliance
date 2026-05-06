using System.Runtime.InteropServices;

namespace KeplerPrint.Helpers
{
    /// <summary>
    /// Multiplataforma ESC/POS byte dispatcher.
    /// Windows → WinSpool API (winspool.Drv)
    /// Linux   → /dev/usb/lp0 (USB directo) o TCP socket (red)
    /// </summary>
    public static class RawPrinterHelper
    {
        // ── WINDOWS: WinSpool P/Invoke ────────────────────────────────────
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

        // ── ENTRY POINT: detecta OS en runtime ───────────────────────────
        /// <summary>
        /// Envía bytes a la impresora.
        /// En Windows: usa winspool (igual que antes).
        /// En Linux:   printerName puede ser:
        ///               - "/dev/usb/lp0"    → USB directo
        ///               - "192.168.1.x:9100" → TCP/IP (red)
        ///               - "CUPS:NombreImpresora" → CUPS via lpr
        /// </summary>
        public static void SendBytesToPrinter(string printerName, byte[] bytes)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SendBytesWindows(printerName, bytes);
            else
                SendBytesLinux(printerName, bytes);
        }

        // ── WINDOWS ───────────────────────────────────────────────────────
        private static void SendBytesWindows(string printerName, byte[] bytes)
        {
            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
                throw new InvalidOperationException(
                    $"No se pudo abrir la impresora '{printerName}'. " +
                    $"Win32Error: {Marshal.GetLastWin32Error()}");
            try
            {
                var di = new DOCINFOA();
                if (!StartDocPrinter(hPrinter, 1, di))
                    throw new InvalidOperationException(
                        $"StartDocPrinter falló. Win32Error: {Marshal.GetLastWin32Error()}");

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

        // ── LINUX ─────────────────────────────────────────────────────────
        private static void SendBytesLinux(string printerName, byte[] bytes)
        {
            // Opción 1: TCP/IP — "192.168.1.105:9100"
            if (printerName.Contains(':') && !printerName.StartsWith("/"))
            {
                var parts = printerName.Split(':');
                var host  = parts[0];
                var port  = int.Parse(parts[1]);
                SendBytesTcp(host, port, bytes);
                return;
            }

            // Opción 2: CUPS via lpr — "CUPS:XP-58"
            if (printerName.StartsWith("CUPS:", StringComparison.OrdinalIgnoreCase))
            {
                var cupsName = printerName[5..];
                SendBytesCups(cupsName, bytes);
                return;
            }

            // Opción 3: Dispositivo USB directo — "/dev/usb/lp0"
            SendBytesUsb(printerName, bytes);
        }

        /// <summary>
        /// Escribe directamente al nodo de dispositivo USB.
        /// Requiere: sudo usermod -aG lp $USER  (o ejecutar como root en Docker)
        /// </summary>
        private static void SendBytesUsb(string devicePath, byte[] bytes)
        {
            if (!File.Exists(devicePath))
                throw new FileNotFoundException(
                    $"Dispositivo de impresora no encontrado: '{devicePath}'. " +
                    "Verifica que la impresora esté conectada por USB y el path sea correcto " +
                    "(típicamente /dev/usb/lp0). Ejecuta: ls /dev/usb/");

            using var fs = new FileStream(
                devicePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None);
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush();
        }

        /// <summary>
        /// Imprime via TCP/IP (impresora con interfaz de red o WiFi).
        /// Puerto estándar ESC/POS: 9100
        /// </summary>
        private static void SendBytesTcp(string host, int port, byte[] bytes)
        {
            using var client = new System.Net.Sockets.TcpClient();
            client.Connect(host, port);
            using var stream = client.GetStream();
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        /// <summary>
        /// Imprime via CUPS usando el comando lpr.
        /// Requiere: sudo apt install cups y la impresora configurada.
        /// </summary>
        private static void SendBytesCups(string printerName, byte[] bytes)
        {
            var tmpFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tmpFile, bytes);
                var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = "lpr",
                    Arguments              = $"-P {printerName} -o raw {tmpFile}",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false
                });
                proc?.WaitForExit(5000);
                if (proc?.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"lpr falló para '{printerName}': {proc?.StandardError.ReadToEnd()}");
            }
            finally
            {
                File.Delete(tmpFile);
            }
        }
    }
}