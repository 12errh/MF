using System;
using System.Numerics;

namespace Mate.Core.Models
{
    public record WindowInfo(IntPtr Handle, Vector2 Position, Vector2 Size, string ClassName);

    public record MonitorInfo(int Index, string Name, Rectangle Bounds);

    public record Rectangle(int X, int Y, int Width, int Height);
}