using GBOG;
using GBOG.CPU;
using System.Text;

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
      string logPath;
      if (args.Length > 2)
      {
        logPath = args[2];
      }
      else
      {
        string headlessDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "HeadlessLogs");
        headlessDir = Path.GetFullPath(headlessDir);
        Directory.CreateDirectory(headlessDir);

        string romBase = Path.GetFileNameWithoutExtension(romPath);
        logPath = Path.Combine(headlessDir, $"{romBase}.txt");
      }
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

          var timeout = TimeSpan.FromSeconds(timeoutSeconds);
          var start = DateTime.UtcNow;

          var task = gb.RunGame();

          int lastTextLen = 0;
          byte lastStatus = 0x80;

          while (!task.IsCompleted)
          {
            if (TryReadTestStatus(gb, out byte status))
            {
              lastTextLen += ReadNewTestText(gb, startOffset: lastTextLen, writer);

              lastStatus = status;
              if (status != 0x80)
              {
                gb.EndGame();
                break;
              }
            }

            if ((DateTime.UtcNow - start) > timeout)
            {
              Console.WriteLine("\nTimeout reached. Terminating emulation.");
              gb.EndGame();
              break;
            }

            Thread.Sleep(5);
          }

          // Give the emulation loop a moment to observe EndGame/timeout.
          if (!task.Wait(TimeSpan.FromSeconds(2)))
          {
            Console.WriteLine("\nEmulation did not exit promptly.");
          }

          if (TryReadTestOutput(gb, out byte finalStatus, out string finalText))
          {
            Console.WriteLine($"\nExitCode(A000)=0x{finalStatus:X2}");
          }
          else
          {
            Console.WriteLine($"\nExitCode(A000)=0x{gb._memory.ReadByte(0xA000):X2}");
          }

          ushort pc = gb.PC;
          byte b0 = gb._memory.ReadByte(pc);
          byte b1 = gb._memory.ReadByte((ushort)(pc + 1));
          byte b2 = gb._memory.ReadByte((ushort)(pc + 2));
          byte b3 = gb._memory.ReadByte((ushort)(pc + 3));
          Console.WriteLine($"PC=0x{pc:X4} SP=0x{gb.SP:X4} DoubleSpeed={(gb.DoubleSpeed ? 1 : 0)} KEY1=0x{gb._memory.ReadByte(0xFF4D):X2} OPC={b0:X2} {b1:X2} {b2:X2} {b3:X2}");
          Console.WriteLine($"Serial: SC writes={gb._memory.SerialControlWrites} starts={gb._memory.SerialTransferStarts}");
        }
      }
      catch (Exception ex)
      {
        if (ex is AggregateException agg)
        {
          agg = agg.Flatten();
          Console.WriteLine($"\nAn error occurred: {agg.Message}");
          foreach (var inner in agg.InnerExceptions)
          {
            Console.WriteLine(inner.ToString());
          }
        }
        else
        {
          Console.WriteLine($"\nAn error occurred: {ex.Message}");
          Console.WriteLine(ex.ToString());
        }
      }
    }

    private static bool TryReadTestStatus(Gameboy gb, out byte status)
    {
      status = 0x80;

      // Signature at A001-A003: DE B0 61
      if (gb._memory.ReadByte(0xA001) != 0xDE || gb._memory.ReadByte(0xA002) != 0xB0 || gb._memory.ReadByte(0xA003) != 0x61)
      {
        return false;
      }

      status = gb._memory.ReadByte(0xA000);
      return true;
    }

    private static int ReadNewTestText(Gameboy gb, int startOffset, TextWriter writer)
    {
      // Precondition: signature already verified.
      // Read newly appended bytes from $A004 onward until we hit the current terminator.
      const int maxChunk = 1024;
      int bytesRead = 0;
      ushort addr = (ushort)(0xA004 + startOffset);
      for (int i = 0; i < maxChunk; i++)
      {
        byte b = gb._memory.ReadByte(addr++);
        if (b == 0)
        {
          break;
        }
        writer.Write((char)b);
        bytesRead++;
      }
      return bytesRead;
    }

    // Kept for final summary output (reads full text once at the end).
    private static bool TryReadTestOutput(Gameboy gb, out byte status, out string text)
    {
      status = 0x80;
      text = string.Empty;

      if (!TryReadTestStatus(gb, out status))
      {
        return false;
      }

      var sb = new StringBuilder(capacity: 256);
      ushort addr = 0xA004;
      const int maxChars = 256 * 1024;
      for (int i = 0; i < maxChars; i++)
      {
        byte b = gb._memory.ReadByte(addr++);
        if (b == 0)
        {
          break;
        }
        sb.Append((char)b);
      }

      text = sb.ToString();
      return true;
    }
  }
}

