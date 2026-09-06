using System;
using System.Diagnostics;
using System.IO;

namespace Lorenz;

public class Ffmpeg : IDisposable
{
  public int Width { get; init; }
  public int Height { get; init; }
  public string OutputFilePath { get; init; }
  private Process? Process { get; set; }
  private Stream? Pipe { get; set; }
  private bool _disposed = false;

  public Ffmpeg(int width, int height, string outputFilePath)
  {
    Width = width;
    Height = height;
    OutputFilePath = outputFilePath;

    Process = Process.Start(new ProcessStartInfo
    {
      FileName = "ffmpeg",
      Arguments = $"-y -f rawvideo -pixel_format rgba -video_size {Width}x{Height} " +
                  $"-framerate {Constants.Fps} -i - -c:v libx264 -pix_fmt yuv420p {OutputFilePath}",
      RedirectStandardInput = true,
      UseShellExecute = false
    })!;

    Pipe = Process.StandardInput.BaseStream;
  }

  public void Write(byte[] data)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(Ffmpeg));

    Pipe?.Write(data, 0, data.Length);
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (_disposed)
      return;

    if (disposing)
    {
      try
      {
        Pipe?.Flush();
        Process?.StandardInput?.Close();
        Process?.WaitForExit();
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Error during dispose: {ex.Message}");
      }
    }

    Pipe?.Dispose();
    Process?.Dispose();

    _disposed = true;
  }

  ~Ffmpeg()
  {
    Dispose(false);
  }
}