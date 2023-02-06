using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioWave
{
    public class Square
    {
        public double rotation;
        public float scale;
        public float amplitude;
        public float alpha;
        public int x;
        public int y;
        public int width;
        public int height;
        public Color color;
        public Color newColor;
        public TimeSpan time;
        public Stopwatch stopwatch;
        public Bitmap texture;
        public static int WindowHeight;
        public static Square NewSquare(int x, int y, int width, int height, float scale, double rotation, float alpha, Color color, bool randomRotation = false)
        {
            var s = new Square()
            {
                alpha = alpha,
                amplitude = 1f,
                color = color,
                height = height,
                rotation = randomRotation ? new Random(DateTime.Now.Millisecond).NextDouble() : rotation,
                scale = scale,
                width = width,
                x = x,
                y = y
            };
            s.texture = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(s.texture))
            {
                g.FillRectangle(new SolidBrush(Color.White), new Rectangle(0, 0, width, height));
            }
            s.time = TimeSpan.Zero;
            s.stopwatch = new Stopwatch();
            s.stopwatch.Start();
            return s;
        }
        public static void SetAmplitude(Square square, float amplitude)
        {
            square.amplitude = Math.Min(amplitude, 1f);
        }
        public static void Update(Square square, bool updateColor = true, bool updateRotation = false)
        {
            TimeSpan add = TimeSpan.FromMilliseconds(100);
            if (square.time <= square.stopwatch.Elapsed - add)
            {
                while (square.time <= square.stopwatch.Elapsed - add)
                { 
                    square.time += add;
                }
                if (updateRotation)
                { 
                    square.rotation += Helper.Radian;
                }
            }
            if (updateColor)
            {
                square.newColor = square.Transtition(square.color, square.amplitude);
            }
        }
        public static void Draw(Square s, int x, int y, int drawHeight, float angle, Graphics graphics)
        {
            Helper.DrawRotate(s.texture, new Rectangle(x, y, s.width, s.width), new Rectangle(0, 0, s.width, s.width), angle, PointF.Empty, s.newColor, Color.Black, RotateType.GraphicsTransform, graphics);
        }
        public static void Draw(Square s, int drawY, int drawWidth, int drawHeight, Graphics graphics)
        {
            using (Graphics g = Graphics.FromImage(s.texture))
            {
                g.RotateTransform((float)s.rotation);
                g.ScaleTransform(s.scale, s.scale);
            }
            graphics.DrawImage(s.texture, new Rectangle(s.x - s.width / 2, drawY, drawWidth, drawHeight), 0, 0, s.width, drawHeight, 
                GraphicsUnit.Pixel,
                s.SetColor(
                    s.newColor.A / 255f,
                    s.newColor.R / 255f,
                    s.newColor.G / 255f,
                    s.newColor.B / 255f
            ));
        }
        private ImageAttributes SetColor(float a, float r, float g, float b)
        {
            ImageAttributes attr = new ImageAttributes();
            attr.SetColorMatrix(
                new ColorMatrix(new float[][]
                {
                    new float[] { r , 0f, 0f, 0f, 0f },
                    new float[] { 0f, g , 0f, 0f, 0f },
                    new float[] { 0f, 0f, b , 0f, 0f },
                    new float[] { 0f, 0f, 0f, a , 0f },
                    new float[] { 0f, 0f, 0f, 0f, 0f }
                }));
            return attr;
        }
        private Color Transtition(Color color, float amplitude)
        {
            double shift = amplitude;
            return Helper.FromDouble(alpha,
                Math.Min(1f, shift * (color.R / 255d)),
                Math.Min(1f, shift * (color.G / 255d)),
                Math.Min(1f, shift * (color.B / 255d)));
        }
    }
    public class _Style
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
        internal static Pen CosineColor(Color color, float amplitude)
        {
            double cos = Math.Abs(Math.Cos(amplitude));
            Color c2 = FromDouble(1f,
                Math.Min(1f, cos * (color.R / 255d)),
                Math.Min(1f, cos * (color.G / 255d)),
                Math.Min(1f, cos * (color.B / 255d)));
            return new Pen(c2, 1f);
        }
        internal static Pen CosineColorAlpha(Color color, float amplitude)
        {
            double cos = Math.Abs(Math.Cos(amplitude));
            double tan = Math.Abs(Math.Tan(amplitude));
            Color c2 = FromDouble(tan,
                Math.Min(1f, cos * (color.R / 255d)),
                Math.Min(1f, cos * (color.G / 255d)),
                Math.Min(1f, cos * (color.B / 255d)));
            return new Pen(c2, 1f);
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

        public static Color FromFloat(float a, float r, float g, float b)
        {
            int A = (int)Math.Min(255f * a, 255),
                R = (int)Math.Min(255f * r, 255),
                G = (int)Math.Min(255f * g, 255),
                B = (int)Math.Min(255f * b, 255);
            return Color.FromArgb(A, R, G, B);
        }
        public static Color FromDouble(double a, double r, double g, double b)
        {
            int A = (int)Math.Max(Math.Min(255d * a, 255), 0),
                R = (int)Math.Max(Math.Min(255d * r, 255), 0),
                G = (int)Math.Max(Math.Min(255d * g, 255), 0),
                B = (int)Math.Max(Math.Min(255d * b, 255), 0);
            return Color.FromArgb(A, R, G, B);
        }
    }
    static class Helper
    {
        public const double Radian = 0.017f;
        public static int circumference(float distance)
        {
            return (int)(Radian * (45f / distance));
        }
        public static double ToRadian(float degrees)
        {
            return degrees * Radian;
        }
        public static float ToDegrees(float radians)
        {
            return radians / (float)Radian;
        }
        public static Color FromFloat(float a, float r, float g, float b)
        {
            int A = (int)Math.Min(255f * a, 255),
                R = (int)Math.Min(255f * r, 255),
                G = (int)Math.Min(255f * g, 255),
                B = (int)Math.Min(255f * b, 255);
            return Color.FromArgb(A, R, G, B);
        }
        public static Color FromDouble(double a, double r, double g, double b)
        {
            int A = (int)Math.Max(Math.Min(255d * a, 255), 0),
                R = (int)Math.Max(Math.Min(255d * r, 255), 0),
                G = (int)Math.Max(Math.Min(255d * g, 255), 0),
                B = (int)Math.Max(Math.Min(255d * b, 255), 0);
            return Color.FromArgb(A, R, G, B);
        }
        public static void DrawRotate(Image image, Rectangle rect, Rectangle sourceRect, float angle, PointF origin, Color newColor, Color transparency, RotateType type, Graphics graphics)
        {
            ImageAttributes attributes = new ImageAttributes();
            ColorMatrix transform = new ColorMatrix(new float[][]
            {
                new float[] { newColor.R / 255f, 0, 0, 0, 0 },
                new float[] { 0, newColor.G / 255f, 0, 0, 0 },
                new float[] { 0, 0, newColor.B / 255f, 0, 0 },
                new float[] { 0, 0, 0, newColor.A / 255f, 0 },
                new float[] { 0, 0, 0, 0, 0 }
            });
            attributes.SetColorMatrix(transform);

            MemoryStream mem = new MemoryStream();
            using (Bitmap clone = (Bitmap)image.Clone())
            {
                clone.MakeTransparent(transparency);
                using (Bitmap bmp = new Bitmap(image.Width, image.Height))
                {
                    using (Graphics gfx = Graphics.FromImage(bmp))
                    {
                        switch (type)
                        {
                            case RotateType.MatrixTransform:
                                var matrix = new Matrix();
                                matrix.RotateAt(angle, origin);
                                gfx.Transform = matrix;
                                break;
                            case RotateType.GraphicsTransform:
                                gfx.TranslateTransform(origin.X, origin.Y);
                                gfx.RotateTransform(angle);
                                gfx.TranslateTransform(-origin.X, -origin.Y);
                                break;
                            default:
                                break;
                        }
                        gfx.DrawImage(clone, Point.Empty);
                        bmp.Save(mem, ImageFormat.Png);
                    }
                }
                graphics.DrawImage(Bitmap.FromStream(mem), rect, sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height, GraphicsUnit.Pixel, attributes);
            }
            mem.Dispose();
        }
    }
    public enum RotateType
    {
        MatrixTransform,
        GraphicsTransform
    }
}
