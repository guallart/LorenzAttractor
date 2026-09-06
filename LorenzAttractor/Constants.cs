namespace Lorenz;

public static class Constants
{
  public const int ParticleCount = 300_000;
  public const string OutputFileName = @"lorenz.mp4";

  public const int Width = 1920;
  public const int Height = 1080;
  public const int Fps = 30;
  public const int TotalFrames = Fps * 60;

  public const int SubstepsPerFrame = 8;
  public const float Dt = 5e-5f;

  public const float Sigma = 10.0f;
  public const float Rho = 28.0f;
  public const float Beta = 8.0f / 3.0f;

  public const float InitRange = 30.0f;
  public const int Seed = 1234;

  public const float OrbitRadius = 75.0f;
  public const float OrbitHeight = 25.0f;
  public const float CenterX = 0.0f;
  public const float CenterY = 0.0f;
  public const float CenterZ = 30.0f;
  public const float FovDegrees = 45.0f;
  public const float NearPlane = 0.1f;
  public const float FarPlane = 1000.0f;

  public const float Exposure = 0.1f; // density hits -> [0,1] brightness
  public const float BloomThreshold = 0.35f;
  public const float BloomStrength = 1.4f;
  public const int BlurRadius = 3;
  public const float BlurSigma = 2.0f;

  // Controls how quickly particle color saturates from blue (slow) to red
  // (fast) as speed increases. Lower = more particles read as "fast" (red).
  public const float SpeedNormalization = 50.0f;

  public const int AxisSamples = 600;
  public const int AxisThickness = 1;
  public const float AxisLength = 100.0f;
}