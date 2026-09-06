using System;
using System.Numerics;

namespace Lorenz;

public class Camera
{
  private float Angle = 0.0f;
  private readonly float AngleStep = 0.005f;

  public static Vector3 Center { get; } = new Vector3(Constants.CenterX, Constants.CenterY, Constants.CenterZ);

  public static Matrix4x4 Projection { get; } = Matrix4x4.CreatePerspectiveFieldOfView(
        Constants.FovDegrees * MathF.PI / 180.0f,
        (float)Constants.Width / Constants.Height,
        Constants.NearPlane,
        Constants.FarPlane);

  public Matrix4x4 GetViewProjection()
  {
    Angle += AngleStep;

    var eye = Center + new Vector3(
        MathF.Cos(Angle) * Constants.OrbitRadius,
        MathF.Sin(Angle) * Constants.OrbitRadius,
        Constants.OrbitHeight);

    return Matrix4x4.CreateLookAt(eye, Center, new Vector3(0, 0, 1)) * Projection;
  }
}