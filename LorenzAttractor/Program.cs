using ILGPU;
using ILGPU.Runtime;

using System;
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
    using var densityR = accelerator.Allocate1D<float>(PixelCount);
    using var densityG = accelerator.Allocate1D<float>(PixelCount);
    using var densityB = accelerator.Allocate1D<float>(PixelCount);
    using var tempA = accelerator.Allocate1D<float>(PixelCount);
    using var tempB = accelerator.Allocate1D<float>(PixelCount);
    using var rgba = accelerator.Allocate1D<byte>(PixelCount * 4);
    using var weights = accelerator.Allocate1D(Init.GaussianWeights());

    var integrate = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<Vec3>>(Kernels.IntegrateKernel);
    var raster = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<Vec3>, ArrayView<float>, ArrayView<float>, ArrayView<float>, Matrix4x4>(Kernels.RasterKernel);
    var threshold = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(Kernels.ThresholdKernel);
    var blur = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int>(Kernels.BlurKernel);
    var combine = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<byte>, int>(Kernels.CombineKernel);
    var alpha = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<byte>>(Kernels.AlphaKernel);

    using var ffmpeg = new Ffmpeg(Constants.Width, Constants.Height, Constants.OutputFileName);
    var frameBytes = new byte[PixelCount * 4];
    var cam = new Camera();

    for (int frame = 0; frame < Constants.TotalFrames; frame++)
    {
      Matrix4x4 viewProjection = cam.GetViewProjection();
      densityR.MemSetToZero();
      densityG.MemSetToZero();
      densityB.MemSetToZero();

      for (int s = 0; s < Constants.SubstepsPerFrame; s++)
      {
        integrate(Constants.ParticleCount, positions.View);
        raster(Constants.ParticleCount, positions.View, densityR.View, densityG.View, densityB.View, viewProjection);
      }

      var channels = new[] { densityR.View, densityG.View, densityB.View };
      for (int c = 0; c < channels.Length; c++)
      {
        threshold(PixelCount, channels[c], tempA.View);
        blur(PixelCount, tempA.View, tempB.View, weights.View, 1);
        blur(PixelCount, tempB.View, tempA.View, weights.View, 0);
        combine(PixelCount, channels[c], tempA.View, rgba.View, c);
      }
      alpha(PixelCount, rgba.View);

      rgba.CopyToCPU(frameBytes);
      Grid.Draw(frameBytes, viewProjection);
      ffmpeg.Write(frameBytes);

      if (frame % 30 == 0)
        Console.WriteLine($"frame {frame + 1}/{Constants.TotalFrames}");
    }

    Console.WriteLine($"wrote {Constants.OutputFileName}");
  }
}