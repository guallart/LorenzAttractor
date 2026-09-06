using ILGPU;
using ILGPU.Runtime;

using System;
using System.Diagnostics;
using System.Numerics;

namespace Lorenz;

static class Program
{
  const int PixelCount = Constants.Width * Constants.Height;

  static void Main()
  {
    using var context = Context.Create(b => b.Default());
    using var accelerator = context.GetPreferredDevice(preferCPU: false).CreateAccelerator(context);

    Console.WriteLine($"Accelerator: {accelerator.Name}");

    using var positions = accelerator.Allocate1D(Init.InitialPositions());
    using var density = accelerator.Allocate1D<float>(PixelCount);
    using var tempA = accelerator.Allocate1D<float>(PixelCount);
    using var tempB = accelerator.Allocate1D<float>(PixelCount);
    using var rgba = accelerator.Allocate1D<byte>(PixelCount * 4);
    using var weights = accelerator.Allocate1D(Init.GaussianWeights());

    var integrate = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<Vec3>>(Kernels.IntegrateKernel);
    var raster = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<Vec3>, ArrayView<float>, Matrix4x4>(Kernels.RasterKernel);
    var threshold = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(Kernels.ThresholdKernel);
    var blur = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int>(Kernels.BlurKernel);
    var combine = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<byte>>(Kernels.CombineKernel);

    var ffmpeg = Process.Start(new ProcessStartInfo
    {
      FileName = "ffmpeg",
      Arguments = $"-y -f rawvideo -pixel_format rgba -video_size {Constants.Width}x{Constants.Height} " +
                    $"-framerate {Constants.Fps} -i - -c:v libx264 -pix_fmt yuv420p {Constants.OutputFileName}",
      RedirectStandardInput = true,
      UseShellExecute = false
    })!;
    var pipe = ffmpeg.StandardInput.BaseStream;

    var frameBytes = new byte[PixelCount * 4];
    var cam = new Camera();

    for (int frame = 0; frame < Constants.TotalFrames; frame++)
    {
      Matrix4x4 viewProjection = cam.GetViewProjection();
      density.MemSetToZero();

      for (int s = 0; s < Constants.SubstepsPerFrame; s++)
      {
        integrate(Constants.ParticleCount, positions.View);
        raster(Constants.ParticleCount, positions.View, density.View, viewProjection);
      }

      threshold(PixelCount, density.View, tempA.View);
      blur(PixelCount, tempA.View, tempB.View, weights.View, 1);
      blur(PixelCount, tempB.View, tempA.View, weights.View, 0);
      combine(PixelCount, density.View, tempA.View, rgba.View);

      rgba.CopyToCPU(frameBytes);
      Axes.Draw(frameBytes, viewProjection);
      pipe.Write(frameBytes, 0, frameBytes.Length);

      if (frame % 30 == 0)
        Console.WriteLine($"frame {frame + 1}/{Constants.TotalFrames}");
    }

    pipe.Flush();
    ffmpeg.StandardInput.Close();
    ffmpeg.WaitForExit();
    Console.WriteLine($"wrote {Constants.OutputFileName}");
  }
}