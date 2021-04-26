using System;

namespace VideoCompiler
{
    public struct HdrColor
    {
        public double R;
        public double G;
        public double B;

        public static HdrColor FromRgb(double R, double G, double B)
        {
            return new HdrColor { R = R, G = G, B = B };
        }

        public static HdrColor FromArgb(int argb)
        {
            return new HdrColor
            {
                R = ((argb >> 16) & 255) / 255d,
                G = ((argb >> 8) & 255) / 255d,
                B = (argb & 255) / 255d
            };
        }

        public static HdrColor operator +(HdrColor left, HdrColor right)
        {
            return new HdrColor
            {
                R = left.R + right.R,
                G = left.G + right.G,
                B = left.B + right.B
            };
        }

        public static HdrColor operator -(HdrColor left, HdrColor right)
        {
            return new HdrColor
            {
                R = left.R - right.R,
                G = left.G - right.G,
                B = left.B - right.B
            };
        }

        public static HdrColor operator *(HdrColor left, HdrColor right)
        {
            return new HdrColor
            {
                R = left.R * right.R,
                G = left.G * right.G,
                B = left.B * right.B
            };
        }

        public static HdrColor operator *(HdrColor left, double right)
        {
            return new HdrColor
            {
                R = left.R * right,
                G = left.G * right,
                B = left.B * right
            };
        }

        public static HdrColor operator /(HdrColor left, HdrColor right)
        {
            return new HdrColor
            {
                R = left.R / right.R,
                G = left.G / right.G,
                B = left.B / right.B
            };
        }

        public static HdrColor operator /(HdrColor left, double right)
        {
            return new HdrColor
            {
                R = left.R / right,
                G = left.G / right,
                B = left.B / right
            };
        }

        public double Length
        {
            get => Math.Sqrt(R * R + G * G + B * B);
        }

        public override string ToString()
        {
            return $"({R}, {G}, {B})";
        }
    }
}
