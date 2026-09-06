namespace Lorenz;

public static class Constants
{
  public const int ParticleCount = 500_000;
  public const string OutputFileName = @"lorenz.mp4";

  public const int Width = 1920;
  public const int Height = 1080;
  public const int Fps = 60;
  public const int TotalFrames = Fps * 5;

  public const int SubstepsPerFrame = 4;
  public const float Dt = 0.002f;

  public const float Sigma = 10.0f;
  public const float Rho = 28.0f;
  public const float Beta = 8.0f / 3.0f;

  public const float InitRange = 20.0f; // initial positions in [-20, 20]^3
  public const int Seed = 1234;

  public const float OrbitRadius = 90.0f;
  public const float OrbitHeight = 30.0f;
  public const float CenterX = 0.0f;
  public const float CenterY = 0.0f;
  public const float CenterZ = 25.0f;
  public const float FovDegrees = 45.0f;
  public const float NearPlane = 0.1f;
  public const float FarPlane = 1000.0f;

  public const float Exposure = 0.05f; // density hits -> [0,1] brightness
  public const float BloomThreshold = 0.35f;
  public const float BloomStrength = 1.4f;
  public const int BlurRadius = 3;
  public const float BlurSigma = 2.0f;

  public const float TintR = 0.55f;
  public const float TintG = 0.78f;
  public const float TintB = 1.00f;

  public const int AxisSamples = 10;
  public const int AxisThickness = 1;
  public const float AxisLength = 20.0f;
}
