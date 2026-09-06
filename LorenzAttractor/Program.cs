using System.Diagnostics;
using System.Numerics;

static class Program
{
  // ------------------------------------------------------------------
  // Parameters. Edit these numbers to change the look of the video.
  // ------------------------------------------------------------------

  const int ParticleCount = 500_000;

  const int Width = 1920;
  const int Height = 1080;
  const int Fps = 60;
  const int TotalFrames = 1800;          // 30 seconds

  const int SubstepsPerFrame = 8;
  const float Dt = 0.002f;

  const float Sigma = 10.0f;
  const float Rho = 28.0f;
  const float Beta = 8.0f / 3.0f;

  const float InitRange = 20.0f;         // initial positions in [-20, 20]^3
  const int Seed = 1234;

  const float OrbitRadius = 90.0f;
  const float OrbitHeight = 30.0f;
  const float CenterX = 0.0f;
  const float CenterY = 0.0f;
  const float CenterZ = 25.0f;
  const float FovDegrees = 45.0f;
  const float NearPlane = 0.1f;
  const float FarPlane = 1000.0f;

  const float Exposure = 0.05f;          // density hits -> [0,1] brightness
  const float BloomThreshold = 0.35f;
  const float BloomStrength = 1.4f;
  const int BlurRadius = 12;
  const float BlurSigma = 5.0f;

  const float TintR = 0.55f;
  const float TintG = 0.78f;
  const float TintB = 1.00f;

  // ------------------------------------------------------------------

  struct Vec3
  {
    public float X, Y, Z;
    public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
  }

  const int PixelCount = Width * Height;

  static void Main()
  {
    using var context = Context.Create(b => b.Default());
    using var accelerator = context.GetPreferredDevice(preferCPU: false)
                                   .CreateAccelerator(context);
    Console.WriteLine($"Accelerator: {accelerator.Name}");

    // Initial particle cloud, generated once on CPU.
    var rng = new Random(Seed);
    var initial = new Vec3[ParticleCount];
    for (int i = 0; i < ParticleCount; i++)
      initial[i] = new Vec3(NextCoord(rng), NextCoord(rng), NextCoord(rng));

    using var positions = accelerator.Allocate1D<Vec3>(ParticleCount);
    positions.CopyFromCPU(initial);

    using var density = accelerator.Allocate1D<float>(PixelCount);
    using var tempA = accelerator.Allocate1D<float>(PixelCount);
    using var tempB = accelerator.Allocate1D<float>(PixelCount);
    using var rgba = accelerator.Allocate1D<byte>(PixelCount * 4);

    var weightsCpu = GaussianWeights();
    using var weights = accelerator.Allocate1D<float>(weightsCpu.Length);
    weights.CopyFromCPU(weightsCpu);

    var integrate = accelerator.LoadAutoGroupedStreamKernel<
        Index1D, ArrayView<Vec3>>(IntegrateKernel);
    var raster = accelerator.LoadAutoGroupedStreamKernel<
        Index1D, ArrayView<Vec3>, ArrayView<float>, Matrix4x4>(RasterKernel);
    var threshold = accelerator.LoadAutoGroupedStreamKernel<
        Index1D, ArrayView<float>, ArrayView<float>>(ThresholdKernel);
    var blur = accelerator.LoadAutoGroupedStreamKernel<
        Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, int>(BlurKernel);
    var combine = accelerator.LoadAutoGroupedStreamKernel<
        Index1D, ArrayView<float>, ArrayView<float>, ArrayView<byte>>(CombineKernel);

    var ffmpeg = Process.Start(new ProcessStartInfo
    {
      FileName = "ffmpeg",
      Arguments = $"-y -f rawvideo -pixel_format rgba -video_size {Width}x{Height} " +
                    $"-framerate {Fps} -i - -c:v libx264 -pix_fmt yuv420p output.mp4",
      RedirectStandardInput = true,
      UseShellExecute = false
    })!;
    var pipe = ffmpeg.StandardInput.BaseStream;

    var center = new Vector3(CenterX, CenterY, CenterZ);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(
        FovDegrees * MathF.PI / 180.0f,
        (float)Width / Height,
        NearPlane,
        FarPlane);

    var frameBytes = new byte[PixelCount * 4];

    for (int frame = 0; frame < TotalFrames; frame++)
    {
      float angle = ((float)frame / TotalFrames) * MathF.PI * 2.0f;
      var eye = center + new Vector3(
          MathF.Cos(angle) * OrbitRadius,
          MathF.Sin(angle) * OrbitRadius,
          OrbitHeight);
      var viewProjection =
          Matrix4x4.CreateLookAt(eye, center, new Vector3(0, 0, 1)) * projection;

      density.MemSetToZero();

      for (int s = 0; s < SubstepsPerFrame; s++)
      {
        integrate(ParticleCount, positions.View);
        raster(ParticleCount, positions.View, density.View, viewProjection);
      }

      threshold(PixelCount, density.View, tempA.View);
      blur(PixelCount, tempA.View, tempB.View, weights.View, 1);
      blur(PixelCount, tempB.View, tempA.View, weights.View, 0);
      combine(PixelCount, density.View, tempA.View, rgba.View);

      rgba.CopyToCPU(frameBytes);
      pipe.Write(frameBytes, 0, frameBytes.Length);

      if (frame % 30 == 0)
        Console.WriteLine($"frame {frame}/{TotalFrames}");
    }

    pipe.Flush();
    ffmpeg.StandardInput.Close();
    ffmpeg.WaitForExit();
    Console.WriteLine("wrote output.mp4");
  }

  static float NextCoord(Random rng) =>
      (float)(rng.NextDouble() * 2.0 - 1.0) * InitRange;

  static float[] GaussianWeights()
  {
    var w = new float[BlurRadius * 2 + 1];
    float sum = 0.0f;
    for (int k = -BlurRadius; k <= BlurRadius; k++)
    {
      float g = MathF.Exp(-(k * k) / (2.0f * BlurSigma * BlurSigma));
      w[k + BlurRadius] = g;
      sum += g;
    }
    for (int i = 0; i < w.Length; i++)
      w[i] /= sum;
    return w;
  }

  // ------------------------------------------------------------------
  // Kernels
  // ------------------------------------------------------------------

  static void IntegrateKernel(Index1D i, ArrayView<Vec3> particles)
  {
    var p = particles[i];
    float dx = Sigma * (p.Y - p.X);
    float dy = p.X * (Rho - p.Z) - p.Y;
    float dz = p.X * p.Y - Beta * p.Z;
    p.X += dx * Dt;
    p.Y += dy * Dt;
    p.Z += dz * Dt;
    particles[i] = p;
  }

  static void RasterKernel(
      Index1D i,
      ArrayView<Vec3> particles,
      ArrayView<float> density,
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

    int px = (int)((ndcX * 0.5f + 0.5f) * Width);
    int py = (int)((0.5f - ndcY * 0.5f) * Height);
    if (px < 0 || px >= Width || py < 0 || py >= Height)
      return;

    Atomic.Add(ref density[py * Width + px], 1.0f);
  }

  static void ThresholdKernel(Index1D i, ArrayView<float> density, ArrayView<float> bright)
  {
    float v = density[i] * Exposure - BloomThreshold;
    bright[i] = v > 0.0f ? v : 0.0f;
  }

  static void BlurKernel(
      Index1D i,
      ArrayView<float> src,
      ArrayView<float> dst,
      ArrayView<float> weights,
      int horizontal)
  {
    int index = i.X;
    int x = index % Width;
    int y = index / Width;

    float sum = 0.0f;
    for (int k = -BlurRadius; k <= BlurRadius; k++)
    {
      int sx = horizontal != 0 ? x + k : x;
      int sy = horizontal != 0 ? y : y + k;
      if (sx < 0 || sx >= Width || sy < 0 || sy >= Height)
        continue;
      sum += src[sy * Width + sx] * weights[k + BlurRadius];
    }
    dst[index] = sum;
  }

  static void CombineKernel(
      Index1D i,
      ArrayView<float> density,
      ArrayView<float> bloom,
      ArrayView<byte> rgba)
  {
    float v = density[i] * Exposure + bloom[i] * BloomStrength;
    if (v > 1.0f) v = 1.0f;
    if (v < 0.0f) v = 0.0f;

    long o = (long)i.X * 4;
    rgba[o + 0] = (byte)(v * TintR * 255.0f);
    rgba[o + 1] = (byte)(v * TintG * 255.0f);
    rgba[o + 2] = (byte)(v * TintB * 255.0f);
    rgba[o + 3] = 255;
  }
}