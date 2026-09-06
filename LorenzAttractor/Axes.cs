using System;
using System.Numerics;

namespace Lorenz;

public static class Axes
{
  private static readonly (Vector3 Dir, byte R, byte G, byte B)[] Specs =
  {
    (new Vector3(1, 0, 0), (byte)255, (byte)70, (byte)70),
    (new Vector3(0, 1, 0), (byte)70, (byte)255, (byte)90),
    (new Vector3(0, 0, 1), (byte)90, (byte)160, (byte)255),
  };

  public static void Draw(byte[] frame, Matrix4x4 vp)
  {
    foreach (var (dir, r, g, b) in Specs)
    {
      Vector3 start = -dir * Constants.AxisLength;
      Vector3 end = dir * Constants.AxisLength;
      DrawLine(frame, vp, start, end, r, g, b);
    }
  }

  private static void DrawLine(byte[] frame, Matrix4x4 vp, Vector3 worldA, Vector3 worldB, byte r, byte g, byte b)
  {
    int? prevX = null;
    int? prevY = null;

    for (int i = 0; i <= Constants.AxisSamples; i++)
    {
      float t = (float)i / Constants.AxisSamples;
      Vector3 p = Vector3.Lerp(worldA, worldB, t);

      if (!TryProject(p, vp, out int px, out int py))
      {
        prevX = null;
        prevY = null;
        continue;
      }

      if (prevX.HasValue && prevY.HasValue)
        DrawSegment(frame, prevX.Value, prevY.Value, px, py, r, g, b);
      else
        PutPixel(frame, px, py, r, g, b);

      prevX = px;
      prevY = py;
    }
  }

  private static bool TryProject(Vector3 p, Matrix4x4 vp, out int px, out int py)
  {
    px = py = 0;

    Vector4 clip = Vector4.Transform(new Vector4(p, 1.0f), vp);
    if (clip.W <= 0.0f)
      return false;

    float ndcX = clip.X / clip.W;
    float ndcY = clip.Y / clip.W;
    if (ndcX < -1.0f || ndcX > 1.0f || ndcY < -1.0f || ndcY > 1.0f)
      return false;

    px = (int)((ndcX * 0.5f + 0.5f) * Constants.Width);
    py = (int)((0.5f - ndcY * 0.5f) * Constants.Height);
    return px >= 0 && px < Constants.Width && py >= 0 && py < Constants.Height;
  }

  private static void DrawSegment(byte[] frame, int x0, int y0, int x1, int y1, byte r, byte g, byte b)
  {
    int dx = Math.Abs(x1 - x0);
    int sx = x0 < x1 ? 1 : -1;
    int dy = -Math.Abs(y1 - y0);
    int sy = y0 < y1 ? 1 : -1;
    int err = dx + dy;

    while (true)
    {
      PutPixel(frame, x0, y0, r, g, b);
      if (x0 == x1 && y0 == y1)
        break;

      int e2 = 2 * err;
      if (e2 >= dy) { err += dy; x0 += sx; }
      if (e2 <= dx) { err += dx; y0 += sy; }
    }
  }

  private static void PutPixel(byte[] frame, int x, int y, byte r, byte g, byte b)
  {
    int t = Constants.AxisThickness;
    for (int oy = -t; oy <= t; oy++)
    {
      int py = y + oy;
      if (py < 0 || py >= Constants.Height)
        continue;

      for (int ox = -t; ox <= t; ox++)
      {
        int px = x + ox;
        if (px < 0 || px >= Constants.Width)
          continue;

        long o = ((long)py * Constants.Width + px) * 4;
        frame[o + 0] = r;
        frame[o + 1] = g;
        frame[o + 2] = b;
        frame[o + 3] = 255;
      }
    }
  }
}