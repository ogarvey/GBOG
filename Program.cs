using GBOG;
using GBOG.CPU;

namespace GBOG
{
  internal static class Program
  {
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
      if (args.Length > 0 && args[0] == "--headless")
      {
        RunHeadless(args);
        return;
      }

      // To customize application configuration such as set high DPI settings or default font,
      // see https://aka.ms/applicationconfiguration.
      
      // Application.Run(new Form1());
      new GameWindow().Run();
    }

    static void RunHeadless(string[] args)
    {
      if (args.Length < 2)
      {
        Console.WriteLine("Usage: --headless <rom_path> [log_path] [timeout_seconds]");
        return;
      }

      string romPath = args[1];
      string logPath = args.Length > 2 ? args[2] : "serial_output.txt";
      int timeoutSeconds = args.Length > 3 && int.TryParse(args[3], out int t) ? t : 60;

      Console.WriteLine($"Starting headless mode...");
      Console.WriteLine($"ROM: {romPath}");
      Console.WriteLine($"Log: {logPath}");
      Console.WriteLine($"Timeout: {timeoutSeconds}s");

      try
      {
        var gb = new Gameboy();
        gb.LoadRom(romPath);

        // Ensure log directory exists
        var logDir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
        {
          Directory.CreateDirectory(logDir);
        }

        using (var writer = new StreamWriter(logPath, false))
        {
          writer.AutoFlush = true;
          gb._memory.SerialDataReceived += (sender, data) =>
          {
            writer.Write(data);
            // Optional: Print to console as well
            // Console.Write(data); 
          };

          var cts = new CancellationTokenSource();
          cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

          try
          {
            // RunGame is async, we wait for it or the timeout
            // Since RunGame doesn't accept a CancellationToken, we just wait on the task
            // and rely on the process terminating or the user killing it if it hangs,
            // but here we use Wait(timeout) to exit the wrapper.
            var task = gb.RunGame();
            task.Wait(cts.Token);
          }
          catch (OperationCanceledException)
          {
            Console.WriteLine("\nTimeout reached. Terminating emulation.");
      ushort pc = gb.PC;
      byte b0 = gb._memory.ReadByte(pc);
      byte b1 = gb._memory.ReadByte((ushort)(pc + 1));
      byte b2 = gb._memory.ReadByte((ushort)(pc + 2));
      byte b3 = gb._memory.ReadByte((ushort)(pc + 3));
      Console.WriteLine($"PC=0x{pc:X4} SP=0x{gb.SP:X4} DoubleSpeed={(gb.DoubleSpeed ? 1 : 0)} KEY1=0x{gb._memory.ReadByte(0xFF4D):X2} OPC={b0:X2} {b1:X2} {b2:X2} {b3:X2}");
      Console.WriteLine($"Serial: SC writes={gb._memory.SerialControlWrites} starts={gb._memory.SerialTransferStarts}");
      Console.WriteLine($"ExitCode(A000)=0x{gb._memory.ReadByte(0xA000):X2}");
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"\nAn error occurred: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
      }
    }
  }
}
