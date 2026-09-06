using System;
using System.Linq;

namespace Lorenz;

public static class Init
{
  private static float NextCoord(Random rng) =>
    (float)(rng.NextDouble() * 2.0 - 1.0) * Constants.InitRange;

  public static float[] GaussianWeights()
  {
    var w = new float[Constants.BlurRadius * 2 + 1];
    float sum = 0.0f;
    for (int k = -Constants.BlurRadius; k <= Constants.BlurRadius; k++)
    {
      float g = MathF.Exp(-(k * k) / (2.0f * Constants.BlurSigma * Constants.BlurSigma));
      w[k + Constants.BlurRadius] = g;
      sum += g;
    }
    for (int i = 0; i < w.Length; i++)
      w[i] /= sum;
    return w;
  }

  public static Vec3[] InitialPositions()
  {
    var rng = new Random(Constants.Seed);
    var initial = Enumerable.Range(0, Constants.ParticleCount)
      .Select(_ => new Vec3(NextCoord(rng), NextCoord(rng), NextCoord(rng)))
      .ToArray();
    return initial;
  }

}
