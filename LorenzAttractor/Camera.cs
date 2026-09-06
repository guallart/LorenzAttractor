using System;
using System.Numerics;

namespace Lorenz;

public static class Camera
{
  public static Vector3 Center { get; } = new Vector3(Constants.CenterX, Constants.CenterY, Constants.CenterZ);

  public static Matrix4x4 Projection { get; } = Matrix4x4.CreatePerspectiveFieldOfView(
        Constants.FovDegrees * MathF.PI / 180.0f,
        (float)Constants.Width / Constants.Height,
        Constants.NearPlane,
        Constants.FarPlane);

  public static Matrix4x4 GetViewProjection(int frame)
  {
    float angle = 0.2f * ((float)frame / Constants.TotalFrames) * MathF.PI * 2.0f;
    var eye = Center + new Vector3(
        MathF.Cos(angle) * Constants.OrbitRadius,
        MathF.Sin(angle) * Constants.OrbitRadius,
        Constants.OrbitHeight);

    return Matrix4x4.CreateLookAt(eye, Center, new Vector3(0, 0, 1)) * Projection;
  }
}