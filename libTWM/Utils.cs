using System;
using System.Collections.Generic;
using System.Drawing;

namespace libTWM
{
    public static class Utils
    {
        /// <summary>
        /// Global random number generator. Initialized once to keep the seed.
        /// </summary>
        private static readonly Random Rng = new Random();

        public static int Choice(int[] a, float[] p, IRandomProvider rng)
        {
            var roll = rng.NextDouble();

            var cum = 0.0;

            for (var i = 0; i < a.Length; i++)
            {
                cum += p[i];

                if (roll < cum)
                {
                    return a[i];
                }
            }

            return 0;
        }

        /// <summary>
        /// Code from http://www.splinter.com.au/converting-hsv-to-rgb-colour-using-c/
        /// </summary>
        /// <param name="h">Hue. Between 0 and 360.</param>
        /// <param name="s">Saturation. Between 0 and 1.</param>
        /// <param name="v">Value. Between 0 and 1.</param>
        /// <returns>Color object</returns>
        public static Color HsvToRgb(double h, double s, double v)
        {
            var H = h;
            while (H < 0) { H += 360; }
            while (H >= 360) { H -= 360; }
            double r, g, b;
            if (v <= 0)
            {
                r = g = b = 0;
            }
            else if (s <= 0)
            {
                r = g = b = v;
            }
            else
            {
                var hf = H / 60.0;
                var i = (int)Math.Floor(hf);
                var f = hf - i;
                var pv = v * (1 - s);
                var qv = v * (1 - s * f);
                var tv = v * (1 - s * (1 - f));
                switch (i)
                {
                    // Red is the dominant color
                    case 0:
                        r = v;
                        g = tv;
                        b = pv;
                        break;
                    // Green is the dominant color
                    case 1:
                        r = qv;
                        g = v;
                        b = pv;
                        break;
                    case 2:
                        r = pv;
                        g = v;
                        b = tv;
                        break;
                    // Blue is the dominant color
                    case 3:
                        r = pv;
                        g = qv;
                        b = v;
                        break;
                    case 4:
                        r = tv;
                        g = pv;
                        b = v;
                        break;
                    // Red is the dominant color
                    case 5:
                        r = v;
                        g = pv;
                        b = qv;
                        break;
                    // Just in case we overshoot on our math by a little, we put these here. 
                    // Since it's a switch it won't slow us down at all to put these here.
                    case 6:
                        r = v;
                        g = tv;
                        b = pv;
                        break;
                    case -1:
                        r = v;
                        g = pv;
                        b = qv;
                        break;
                    // The color is not defined, we should throw an error.
                    default:
                        r = g = b = v; // Just pretend it's black/white
                        break;
                }
            }
            return Color.FromArgb(
                Clamp((int)(r * 255.0)),
                Clamp((int)(g * 255.0)),
                Clamp((int)(b * 255.0)));
        }

        /// <summary>
        /// Clamp a value to 0-255
        /// </summary>
        public static int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }
    }
}
