using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioWave
{
    public class Style
    {
        const double Radian = 0.017d;
        static double amplitude = MainWindow.Instance.Height / 2d;
        double y = 1d;
        double cos;
        double angle;
        float radius;
        int distance;
        internal static void DrawLine(int x, int y, int center, bool reverse, Color one, Graphics graphics)
        {
            if (!reverse)
            {
                var pen = new Pen(new SolidBrush(one));
                graphics.DrawLine(pen, x, center + (y - 255), x, center);
            }
            else
            {
                var pen = new Pen(new SolidBrush(one));
                graphics.DrawLine(pen, x, center - (y - 255), x, center);
            }
        }
        internal static void DrawLine(int x, int y, int width, int center, bool reverse, Color one, Graphics graphics)
        {
            if (!reverse)
            {
                for (double n = y; n > 0d; n -= y / amplitude)
                {
                    Color c2 = FromDouble(1f,
                            Math.Min(1f, one.R / 255d - n / amplitude * (one.R / 255d)),
                            Math.Min(1f, one.G / 255d - n / amplitude * (one.G / 255d)),
                            Math.Min(1f, one.B / 255d - n / amplitude * (one.B / 255d)));
                    var pen = new Pen(new SolidBrush(c2));
                    graphics.DrawLine(pen, x, center, x + width, center + y);
                }
            }
            else
            {
                for (double n = y; n > 0d; n -= y / amplitude)
                {
                    Color c2 = FromDouble(1f,
                            Math.Min(1f, one.R / 255d - n / amplitude * (one.R / 255d)),
                            Math.Min(1f, one.G / 255d - n / amplitude * (one.G / 255d)),
                            Math.Min(1f, one.B / 255d - n / amplitude * (one.B / 255d)));
                    var pen = new Pen(new SolidBrush(c2));
                    graphics.DrawLine(pen, x, center, x + width, center - y);
                }
            }
        }
        internal static void DrawGradient(int x, int y, int width, int center, bool reverse, Color one, Graphics graphics)
        {
            if (!reverse)
            {
                for (int j = center; j < center + y; j++)
                {
                    for (double n = y; n > 0d; n -= y / amplitude)
                    {
                        Color c2 = FromDouble(1f,
                                Math.Min(1f, one.R / 255d - n / amplitude * (one.R / 255d)),
                                Math.Min(1f, one.G / 255d - n / amplitude * (one.G / 255d)),
                                Math.Min(1f, one.B / 255d - n / amplitude * (one.B / 255d)));
                        var pen = new Pen(new SolidBrush(c2));
                        graphics.DrawLine(pen, x, center, x + width, center + j);
                    }
                }
            }
            else
            {
                for (int j = center; j > center - y; j--)
                {
                    for (double n = y; n > 0d; n -= y / amplitude)
                    {
                        Color c2 = FromDouble(1f,
                                Math.Min(1f, one.R / 255d - n / amplitude * (one.R / 255d)),
                                Math.Min(1f, one.G / 255d - n / amplitude * (one.G / 255d)),
                                Math.Min(1f, one.B / 255d - n / amplitude * (one.B / 255d)));
                        var pen = new Pen(new SolidBrush(c2));
                        graphics.DrawLine(pen, x, j, x + width, j);
                    }
                }
            }
        }
        /*
        private void DrawCosineWave(Color one, Color two, Graphics graphics)
        {
            for (int j = 0; j < gradient.Height; j++)
            {
                for (int n = 0; n < gradient.Height / 10; n += gradient.Height / 10)
                {
                    cos = y * n + amplitude * Math.Sin(angle += 0.017d);
                    Color c = two;
                    Color c2 = FromFloat(1f,
                            Math.Min(1f, one.R / 255f * (float)(Math.Cos(angle) + 1f)),
                            Math.Min(1f, one.G / 255f * (float)(Math.Cos(angle) + 1f)),
                            Math.Min(1f, one.B / 255f * (float)(Math.Cos(angle) + 1f)));
                    var pen = new Pen(new SolidBrush(c2));
                    graphics.DrawLine(pen, 0, j, gradient.Width, j);
                }
            }
            if (angle >= double.MaxValue - 0.017d)
                angle = 0;
        }*/

        private static Color FromFloat(float a, float r, float g, float b)
        {
            int A = (int)Math.Min(255f * a, 255),
                R = (int)Math.Min(255f * r, 255),
                G = (int)Math.Min(255f * g, 255),
                B = (int)Math.Min(255f * b, 255);
            return Color.FromArgb(A, R, G, B);
        }
        private static Color FromDouble(double a, double r, double g, double b)
        {
            int A = (int)Math.Max(Math.Min(255d * a, 255), 0),
                R = (int)Math.Max(Math.Min(255d * r, 255), 0),
                G = (int)Math.Max(Math.Min(255d * g, 255), 0),
                B = (int)Math.Max(Math.Min(255d * b, 255), 0);
            return Color.FromArgb(A, R, G, B);
        }
    }
    class _helper
    {
        const float Radian = 0.017f;
        private int circumference(float distance)
        {
            return (int)(Radian * (45f / distance));
        }
        private float ToRadian(float degrees)
        {
            return degrees * Radian;
        }
        private float ToDegrees(float radians)
        {
            return radians / Radian;
        }
    }
}
