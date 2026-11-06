// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Cameras;

namespace ElysianOutpost.Core.Tests.Cameras;

/// <summary>
/// Calculates one frame of the legacy script <c>CameraMovements.Update</c> with the math of Unity, in double precision.
/// The tests compare <see cref="FreeFlyCamera.Look"/> with this emulation.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>Quaternion</c>: (x, y, z, w) with the Hamilton product. <c>Quaternion.AngleAxis</c> uses the half angle.</item>
/// <item><c>Transform.Rotate(axis, angle)</c> in <c>Space.Self</c>: rotation = normalize(rotation × AngleAxis(angle, axis)).</item>
/// <item>
/// <c>Quaternion.eulerAngles</c> in the order Z, X, Y (q = qY × qX × qZ). The usual formula is x = asin(-m12), y = atan2(m02, m22)
/// and z = atan2(m10, m11). Near the vertical (|sin x| ≥ 1 - 2e-6), Unity uses x = ±90, y = atan2(±m01, m00) and z = 0.
/// Then Unity makes each angle positive, with a limit of -0.0001 radians.
/// </item>
/// <item>The script sets z to 0, changes x to the range -180 to 180, and limits it to the pitch range.</item>
/// </list>
/// The cursor movement of GLFW has the Y axis down. The mouse movement of Unity has the Y axis up.
/// </remarks>
internal static class UnityCameraEmulation
{
    /// <summary>
    /// The limit of |sin x| for the usual formula of the Euler angles.
    /// </summary>
    public const double VerticalLimit = 1.0 - 2e-6;

    private const double DegreesToRadians = Math.PI / 180.0;

    /// <summary>
    /// Calculates the yaw and the pitch after one frame of the legacy script.
    /// </summary>
    /// <param name="yaw">The yaw before the frame, in degrees.</param>
    /// <param name="pitch">The pitch before the frame, in degrees.</param>
    /// <param name="cursorX">The cursor movement to the right, in pixels.</param>
    /// <param name="cursorY">The cursor movement down, in pixels (GLFW axes).</param>
    /// <param name="settings">The sensitivity and the pitch limits.</param>
    /// <returns>The yaw (0 to 360), the pitch after the limit, and <c>true</c> if Unity used the formula for the vertical.</returns>
    public static (double Yaw, double Pitch, bool NearVertical) Frame(double yaw, double pitch, double cursorX, double cursorY, FreeFlyCameraSettings settings)
    {
        double viewX = cursorX * settings.Sensitivity;
        double viewY = -cursorY * settings.Sensitivity;

        Quaternion rotation = Euler(pitch, yaw, 0.0);
        rotation = Normalize(Multiply(rotation, AngleAxis(viewX, 0, 1, 0)));
        rotation = Normalize(Multiply(rotation, AngleAxis(-viewY, 1, 0, 0)));

        (double x, double y, _, bool nearVertical) = EulerAngles(rotation);
        if (x > 180.0)
        {
            x -= 360.0;
        }

        x = Math.Clamp(x, settings.MinPitch, settings.MaxPitch);
        double newYaw = y % 360.0;
        return (newYaw < 0.0 ? newYaw + 360.0 : newYaw, x, nearVertical);
    }

    private static Quaternion Euler(double x, double y, double z) =>
        Multiply(Multiply(AngleAxis(y, 0, 1, 0), AngleAxis(x, 1, 0, 0)), AngleAxis(z, 0, 0, 1));

    private static Quaternion AngleAxis(double degrees, double axisX, double axisY, double axisZ)
    {
        (double sin, double cos) = Math.SinCos(degrees * DegreesToRadians * 0.5);
        return new Quaternion(axisX * sin, axisY * sin, axisZ * sin, cos);
    }

    private static Quaternion Multiply(Quaternion a, Quaternion b) => new(
        (a.W * b.X) + (a.X * b.W) + (a.Y * b.Z) - (a.Z * b.Y),
        (a.W * b.Y) + (a.Y * b.W) + (a.Z * b.X) - (a.X * b.Z),
        (a.W * b.Z) + (a.Z * b.W) + (a.X * b.Y) - (a.Y * b.X),
        (a.W * b.W) - (a.X * b.X) - (a.Y * b.Y) - (a.Z * b.Z));

    private static Quaternion Normalize(Quaternion q)
    {
        double length = Math.Sqrt((q.X * q.X) + (q.Y * q.Y) + (q.Z * q.Z) + (q.W * q.W));
        return new Quaternion(q.X / length, q.Y / length, q.Z / length, q.W / length);
    }

    private static (double X, double Y, double Z, bool NearVertical) EulerAngles(Quaternion q)
    {
        double m00 = 1 - (2 * ((q.Y * q.Y) + (q.Z * q.Z)));
        double m01 = 2 * ((q.X * q.Y) - (q.W * q.Z));
        double m02 = 2 * ((q.X * q.Z) + (q.W * q.Y));
        double m10 = 2 * ((q.X * q.Y) + (q.W * q.Z));
        double m11 = 1 - (2 * ((q.X * q.X) + (q.Z * q.Z)));
        double m12 = 2 * ((q.Y * q.Z) - (q.W * q.X));
        double m22 = 1 - (2 * ((q.X * q.X) + (q.Y * q.Y)));

        double sinX = -m12;
        double x;
        double y;
        double z;
        bool nearVertical = Math.Abs(sinX) >= VerticalLimit;
        if (!nearVertical)
        {
            x = Math.Asin(sinX);
            y = Math.Atan2(m02, m22);
            z = Math.Atan2(m10, m11);
        }
        else if (sinX > 0)
        {
            x = Math.PI / 2;
            y = Math.Atan2(m01, m00);
            z = 0;
        }
        else
        {
            x = -Math.PI / 2;
            y = Math.Atan2(-m01, m00);
            z = 0;
        }

        return (MakePositive(x / DegreesToRadians), MakePositive(y / DegreesToRadians), MakePositive(z / DegreesToRadians), nearVertical);
    }

    private static double MakePositive(double degrees)
    {
        const double negativeLimit = -0.0001 / DegreesToRadians;
        if (degrees < negativeLimit)
        {
            return degrees + 360.0;
        }

        return degrees > 360.0 + negativeLimit ? degrees - 360.0 : degrees;
    }

    private readonly record struct Quaternion(double X, double Y, double Z, double W);
}
