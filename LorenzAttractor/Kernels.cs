using ILGPU;

using System;
using System.Numerics;

namespace Lorenz;

public static class Kernels
{
  public static void IntegrateKernel(Index1D i, ArrayView<Vec3> particles)
  {
    var p = particles[i];
    float dx = Constants.Sigma * (p.Y - p.X);
    float dy = p.X * (Constants.Rho - p.Z) - p.Y;
    float dz = p.X * p.Y - Constants.Beta * p.Z;
    p.X += dx * Constants.Dt;
    p.Y += dy * Constants.Dt;
    p.Z += dz * Constants.Dt;
    particles[i] = p;
  }

  public static void RasterKernel(
      Index1D i,
      ArrayView<Vec3> particles,
      ArrayView<float> densityR,
      ArrayView<float> densityG,
      ArrayView<float> densityB,
      Matrix4x4 vp)
  {
    var p = particles[i];

    float w = p.X * vp.M14 + p.Y * vp.M24 + p.Z * vp.M34 + vp.M44;
    if (w <= 0.0f)
      return;

    float cx = p.X * vp.M11 + p.Y * vp.M21 + p.Z * vp.M31 + vp.M41;
    float cy = p.X * vp.M12 + p.Y * vp.M22 + p.Z * vp.M32 + vp.M42;

    float ndcX = cx / w;
    float ndcY = cy / w;
    if (ndcX < -1.0f || ndcX > 1.0f || ndcY < -1.0f || ndcY > 1.0f)
      return;

    int px = (int)((ndcX * 0.5f + 0.5f) * Constants.Width);
    int py = (int)((0.5f - ndcY * 0.5f) * Constants.Height);
    if (px < 0 || px >= Constants.Width || py < 0 || py >= Constants.Height)
      return;

    // Speed = magnitude of the Lorenz flow vector at this point (how fast the
    // particle is currently moving through phase space), used to color it.
    float dx = Constants.Sigma * (p.Y - p.X);
    float dy = p.X * (Constants.Rho - p.Z) - p.Y;
    float dz = p.X * p.Y - Constants.Beta * p.Z;
    float speed = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

    // Compress unbounded speed into [0,1]; SpeedNormalization tunes sensitivity.
    float t = speed / (speed + Constants.SpeedNormalization);

    SpeedToColor(t, out float cr, out float cg, out float cb);

    int idx = py * Constants.Width + px;
    Atomic.Add(ref densityR[idx], cr);
    Atomic.Add(ref densityG[idx], cg);
    Atomic.Add(ref densityB[idx], cb);
  }

  // Maps normalized speed t in [0,1] to an RGB weight using a blue -> cyan ->
  // green -> yellow -> red gradient (slow -> fast).
  private static void SpeedToColor(float t, out float r, out float g, out float b)
  {
    if (t < 0.25f)
    {
      float f = t / 0.25f;
      r = 0.0f; g = f; b = 1.0f;
    }
    else if (t < 0.5f)
    {
      float f = (t - 0.25f) / 0.25f;
      r = 0.0f; g = 1.0f; b = 1.0f - f;
    }
    else if (t < 0.75f)
    {
      float f = (t - 0.5f) / 0.25f;
      r = f; g = 1.0f; b = 0.0f;
    }
    else
    {
      float f = (t - 0.75f) / 0.25f;
      r = 1.0f; g = 1.0f - f; b = 0.0f;
    }
  }

  public static void ThresholdKernel(Index1D i, ArrayView<float> density, ArrayView<float> bright)
  {
    float v = density[i] * Constants.Exposure - Constants.BloomThreshold;
    bright[i] = v > 0.0f ? v : 0.0f;
  }

  public static void BlurKernel(
      Index1D i,
      ArrayView<float> src,
      ArrayView<float> dst,
      ArrayView<float> weights,
      int horizontal)
  {
    int index = i.X;
    int x = index % Constants.Width;
    int y = index / Constants.Width;

    float sum = 0.0f;
    for (int k = -Constants.BlurRadius; k <= Constants.BlurRadius; k++)
    {
      int sx = horizontal != 0 ? x + k : x;
      int sy = horizontal != 0 ? y : y + k;
      if (sx < 0 || sx >= Constants.Width || sy < 0 || sy >= Constants.Height)
        continue;
      sum += src[sy * Constants.Width + sx] * weights[k + Constants.BlurRadius];
    }
    dst[index] = sum;
  }

  public static void CombineKernel(
      Index1D i,
      ArrayView<float> density,
      ArrayView<float> bloom,
      ArrayView<byte> rgba,
      int channelOffset)
  {
    float v = density[i] * Constants.Exposure + bloom[i] * Constants.BloomStrength;
    v = v / (1.0f + v);

    long o = (long)i.X * 4 + channelOffset;
    rgba[o] = (byte)(v * 255.0f);
  }

  public static void AlphaKernel(Index1D i, ArrayView<byte> rgba)
  {
    rgba[(long)i.X * 4 + 3] = 255;
  }
}