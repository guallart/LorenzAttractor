using System;
using System.Collections.Generic;
using System.Numerics;

namespace Lorenz;

public static class Grid
{
  private const float Extent = 120.0f;        // half-size of the grid in world units
  private const float Spacing = 20.0f;        // gap between lines
  private const float PlaneZ = 0.0f;          // height of the plane
  private const float FadeDistance = 220.0f;  // lines dim with distance at this scale
  private const int Samples = 96;             // projection samples per line

  private readonly record struct Line(Vector3 P0, Vector3 P1, byte R, byte G, byte B, float Alpha);

  private static readonly Line[] Lines = Build();

  private static Line[] Build()
  {
    var lines = new List<Line>();
    int n = (int)(Extent / Spacing);

    for (int i = -n; i <= n; i++)
    {
      float c = i * Spacing;
      bool center = i == 0;
      byte v = center ? (byte)120 : (byte)70;
      float a = center ? 0.9f : 0.5f;

      lines.Add(new Line(new Vector3(c, -Extent, PlaneZ), new Vector3(c, Extent, PlaneZ), v, v, (byte)(v + 40), a));
      lines.Add(new Line(new Vector3(-Extent, c, PlaneZ), new Vector3(Extent, c, PlaneZ), v, v, (byte)(v + 40), a));
    }

    lines.Add(new Line(new Vector3(0, 0, PlaneZ), new Vector3(0, 0, PlaneZ + 60.0f), 90, 160, 255, 0.7f));

    return lines.ToArray();
  }

  public static void Draw(byte[] frame, Matrix4x4 vp)
  {
    foreach (var line in Lines)
      DrawLine(frame, vp, line);
  }

  private static void DrawLine(byte[] frame, Matrix4x4 vp, in Line line)
  {
    int prevX = 0, prevY = 0;
    float prevFade = 0.0f;
    bool hasPrev = false;

    for (int i = 0; i <= Samples; i++)
    {
      Vector3 p = Vector3.Lerp(line.P0, line.P1, (float)i / Samples);

      if (!TryProject(p, vp, out int px, out int py, out float fade))
      {
        hasPrev = false;
        continue;
      }

      if (hasPrev)
        DrawSegment(frame, prevX, prevY, px, py, line, line.Alpha * 0.5f * (prevFade + fade));
      else
        Plot(frame, px, py, line, line.Alpha * fade);

      prevX = px;
      prevY = py;
      prevFade = fade;
      hasPrev = true;
    }
  }

  private static bool TryProject(Vector3 p, Matrix4x4 vp, out int px, out int py, out float fade)
  {
    px = py = 0;
    fade = 0.0f;

    Vector4 clip = Vector4.Transform(new Vector4(p, 1.0f), vp);
    if (clip.W <= 0.0f)
      return false;

    float ndcX = clip.X / clip.W;
    float ndcY = clip.Y / clip.W;
    if (ndcX < -1.0f || ndcX > 1.0f || ndcY < -1.0f || ndcY > 1.0f)
      return false;

    px = (int)((ndcX * 0.5f + 0.5f) * Constants.Width);
    py = (int)((0.5f - ndcY * 0.5f) * Constants.Height);
    if (px < 0 || px >= Constants.Width || py < 0 || py >= Constants.Height)
      return false;

    // clip.W is the view-space distance along the camera forward axis.
    fade = MathF.Exp(-clip.W / FadeDistance);
    return true;
  }

  private static void DrawSegment(byte[] frame, int x0, int y0, int x1, int y1, in Line line, float alpha)
  {
    int dx = Math.Abs(x1 - x0);
    int sx = x0 < x1 ? 1 : -1;
    int dy = -Math.Abs(y1 - y0);
    int sy = y0 < y1 ? 1 : -1;
    int err = dx + dy;

    while (true)
    {
      Plot(frame, x0, y0, line, alpha);
      if (x0 == x1 && y0 == y1)
        break;

      int e2 = 2 * err;
      if (e2 >= dy) { err += dy; x0 += sx; }
      if (e2 <= dx) { err += dx; y0 += sy; }
    }
  }

  // Additive blend: the grid glows into the frame and never darkens particles.
  private static void Plot(byte[] frame, int x, int y, in Line line, float alpha)
  {
    if (x < 0 || x >= Constants.Width || y < 0 || y >= Constants.Height || alpha <= 0.0f)
      return;

    long o = ((long)y * Constants.Width + x) * 4;
    Add(frame, o + 0, line.R * alpha);
    Add(frame, o + 1, line.G * alpha);
    Add(frame, o + 2, line.B * alpha);
    frame[o + 3] = 255;
  }

  private static void Add(byte[] frame, long offset, float amount)
  {
    int v = frame[offset] + (int)amount;
    frame[offset] = (byte)(v > 255 ? 255 : v);
  }
}