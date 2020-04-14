using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;

namespace TheWordMachine
{
    internal static class Program
    {
        private static int MinWordSize { get; set; } = 3;
        private static int MaxWordSize { get; set; } = 12;
        private static int WordsPerSize { get; set; } = 100;
        private static bool ExcludeExistingWords { get; set; }
        private static string TheEncoding { get; set; } = "UTF-8";

        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(
                    "Usage: thewordmachine [option=value] [switches] file1 file2...\n" +
                    "Options:\n" +
                    "\t-min\t--min-word-size\tSets the minimum size of generated words. Default: 4\n" +
                    "\t-max\t--max-word-size\tSets the minimum size of generated words. Default: 12\n" +
                    "\t-wps\t--words-per-size\tSets the number of words to be generated for each size. Default: 100\n" +
                    "\t-enc\t--encoding\tSets the encoding of the input files. Default: UTF-8\n" +
                    "Switches:\n" +
                    "\t-noex\t--exclude-existing\tExclude words already present in the dictionary. Default: No\n" +
                    "By default, the files are searched in the running directory and in the data\\ directory (in the running directory).\n" +
                    "Example usage: thewordmachine -min=5 -max=8 -wps=20 EN.txt FR.txt IT.txt\n");
                Environment.Exit(1);
            }

            var pars = args.Where(x => x[0] == '-');
            var inps = args.Where(x => x[0] != '-').ToArray();

            if (!inps.Any())
            {
                Console.WriteLine("Error: no input provided, exiting");
                return;
            }

            foreach (var parp in pars)
            {
                var par = parp.ToLower();
                var sp = par.Split('=');
                // switches
                switch (sp[0])
                {
                    case "-noex":
                    case "--exclude-existing":
                        ExcludeExistingWords = true;
                        continue;
                }
                if (sp.Length > 1)
                {
                    switch (sp[0])
                    {
                        case "-enc":
                        case "--encoding":
                            /*if (sp[1].ToUpper() == "UTF-8")
                            {
                                Console.WriteLine("UTF-8 is not supported for the moment. Please convert your file to another encoding, for example ISO-8859-1 or Windows-1252.");
                            }
                            else*/ TheEncoding = sp[1];
                            continue;
                    }
                    int v;
                    if (int.TryParse(sp[1], out v))
                    {
                        switch (sp[0])
                        {
                            case "-min":
                            case "--min-word-size":
                                MinWordSize = v;
                                break;
                            case "-max":
                            case "--max-word-size":
                                MaxWordSize = v;
                                break;
                            case "-wps":
                            case "--words-per-size":
                                WordsPerSize = v;
                                break;
                        }
                    }
                    else Console.WriteLine("Invalid number: " + sp[2]);
                }
                else Console.WriteLine("Expected value");
            }

            foreach (var s in inps)
            {
                GenMots(s);
            }
        }

        private static void GenMots(string n)
        {
            Console.WriteLine("Generating words for file: " + n);
            var outp = Path.Combine(Environment.CurrentDirectory, "output", n);
            if (Directory.Exists(outp))
            {
                Directory.Delete(outp, true);
            }
            Directory.CreateDirectory(outp);
            var count = new int[256][][];
            var charMap = new List<int> {0, '\n'};
            /*
            [0] = \0 (word start)
            [1] = \n (word end)
            [2] = A
            [3] = B
            */
            for (var i = 0; i < 256; i++)
            {
                count[i] = new int[256][];
                for (var j = 0; j < 256; j++)
                {
                    count[i][j] = new int[256];
                }
            }
            var fn = n;
            if (!File.Exists(fn))
            {
                fn = Path.Combine("data", fn);
            }
            if (!File.Exists(fn))
            {
                Console.WriteLine("File not found: " + n + " ; skipping");
                return;
            }
            Encoding enc;
            try
            {
                enc = Encoding.GetEncoding(TheEncoding);
            }
            catch
            {
                enc = Encoding.GetEncoding("ISO-8859-1");
            }
            var lines = File.ReadAllLines(fn, enc);
            Console.WriteLine("0 / " + lines.Length);
            var cnt = 0;
            foreach (var l2 in lines)
            {
                var l = l2;
                var i = 0;
                var j = 0;
                foreach (var k in (l.ToLower() + "\n").Select(x => x == ' ' ? 0 : x))
                {
                    if(!charMap.Contains(k)) charMap.Add(k);
                    count[charMap.IndexOf(i)][charMap.IndexOf(j)][charMap.IndexOf(k)]++;
                    i = j;
                    j = k;
                }
                cnt++;
                if (cnt % 1000 == 0)
                {
                    Console.CursorTop--;
                    Console.WriteLine(cnt + " / " + lines.Length);
                }
            }

            Console.CursorTop--;
            Console.WriteLine(lines.Length + " / " + lines.Length);

            Console.WriteLine("Generating probability matrix");

            var count2D = CalculateTheFuckingSumAxis0(count);
            var proba2D = Divide(count2D, Transpose2(Tile2(CalculateTheFuckingSum(Transpose2(count2D)))));
            var alpha = 0.33f;
            var proba2Da = Pow(proba2D, alpha);
            var charMapFiltered = charMap
                .Where(x => !char.IsDigit((char)x))
                .ToList();
            var bsize = (charMapFiltered.Count - 2) * 24 + 24;
            var img = new Bitmap(bsize, bsize + 24);
            var font = new Font("Trebuchet MS", 20, FontStyle.Regular, GraphicsUnit.Pixel);
#if TRIER_ACCENTS
            var cm2_1 = charMapFiltered.Select(x => ((char)x).ToString()).ToList();
            cm2_1.Sort();
            var cm2 = cm2_1.Select(x => (int) x[0]).ToList();
#else
            var cm2 = charMapFiltered.ToList();
            cm2.Sort();
#endif
            using (var gfx = Graphics.FromImage(img))
            {
                gfx.CompositingQuality = CompositingQuality.HighQuality;
                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
                gfx.SmoothingMode = SmoothingMode.HighQuality;
                gfx.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                gfx.Clear(Color.White);
                gfx.DrawString("P", font, Brushes.Black, new RectangleF(0, 0, 24, 24));
                gfx.DrawString("=", font, Brushes.Black, new RectangleF(24, 0, 24, 24));
                gfx.DrawString("0", font, Brushes.Black, new RectangleF(48, 0, 24, 24));
                gfx.FillRectangle(Brushes.Black, 72, 0, 24, 24);
                for (var i = 0; i < bsize - 120; i++)
                {
                    gfx.FillRectangle(new SolidBrush(HsvToRgb((1 - i / (double)(bsize - 120)) * 240, 1, 1)), 96 + i, 0, 1, 24);
                }
                gfx.DrawString("1", font, Brushes.Black, new RectangleF(bsize - 24, 0, 24, 24));
                for (var i = 2; i < charMapFiltered.Count; i++)
                {
                    gfx.DrawString(((char)charMapFiltered[i]).ToString(), font, new SolidBrush(Color.Black), new RectangleF(0, 24 * (cm2.IndexOf(charMapFiltered[i])), 24, 24));

                    for (var j = 2; j < charMapFiltered.Count; j++)
                    {
                        gfx.DrawString(((char)charMapFiltered[j]).ToString(), font, new SolidBrush(Color.Black), new RectangleF(24 * (cm2.IndexOf(charMapFiltered[j]) - 1), 24, 24, 24));
                        var prob = proba2Da[i][j];
                        var color = Color.Black;
                        // probability is explicitely set to zero iff it never happens
                        // ReSharper disable once CompareOfFloatsByEqualityOperator
                        if(prob != 0)
                            color = HsvToRgb((1 - prob) * 240, 1, 1);
                        gfx.FillRectangle(new SolidBrush(color), 24 * (cm2.IndexOf(charMapFiltered[j]) - 1), 24 * (cm2.IndexOf(charMapFiltered[i])), 24, 24);
                    }
                }
            }
            img.Save(Path.Combine(outp, "matrix.png"), ImageFormat.Png);

            Console.WriteLine("Generating words");
            var sum = CalculateTheFuckingSumAxis2(count);
            var sumTiled = Transpose3(Tile3(Transpose2(sum)));
            var proba = Divide(count, sumTiled);

            for (var size = MinWordSize; size <= MaxWordSize; size++)
            {
                Console.WriteLine(" for size " + size);
                var genCount = 0;
                int i, j, k;
                string curWord;
                var genWords = new string[WordsPerSize];
                while (genCount < genWords.Length)
                {
                    i = 0;
                    j = 0;
                    curWord = "";
                    while (j != 1 && curWord.Length <= size + 1)
                    {
                        k = Choice(Enumerable.Range(0, 256).ToArray(), proba[i][j]);
                        if (k == 0) continue;
                        curWord += (char)charMap[k];
                        i = j;
                        j = k;
                    }
                    if (curWord.Length == size + 1)
                    {
                        var x = curWord.Substring(0, curWord.Length - 1);
                        if (genWords.Contains(x)) continue;
                        if (lines.Contains(x))
                            if (ExcludeExistingWords) continue;
                            else x += "*";
                        genWords[genCount] = x;
                        genCount++;
                    }
                }
                File.WriteAllLines(Path.Combine(outp, "words_" + size + ".txt"), genWords);
            }
        }

        private static int[] Sum(int[][] arr)
        {
            var result = new int[256];

            for (var i = 0; i < 256; i++)
            {
                var s = 0;

                for (var j = 0; j < 256; j++)
                {
                    s += arr[j][i];
                }

                result[i] = s;
            }

            return result;
        }

        private static int[][] SumAxis0(int[][][] arr)
        {
            var result = new int[256][];

            for (var i = 0; i < 256; i++)
            {
                result[i] = new int[256];
                for (var j = 0; j < 256; j++)
                {
                    var s = 0;
                    for (var k = 0; k < 256; k++)
                    {
                        s += arr[k][i][j];
                    }
                    result[i][j] = s;
                }
            }

            return result;
        }

        private static int[][] SumAxis2(int[][][] arr)
        {
            var result = new int[256][];

            for (var i = 0; i < 256; i++)
            {
                result[i] = new int[256];
                for (var j = 0; j < 256; j++)
                {
                    var s = 0;
                    for (var k = 0; k < 256; k++)
                    {
                        s += arr[i][j][k];
                    }
                    result[i][j] = s;
                }
            }

            return result;
        }

        private static int[][] Transpose2(int[][] array)
        {
            var result = new int[256][];

            for (var i = 0; i < 256; i++)
            {
                result[i] = new int[256];
                for (var j = 0; j < 256; j++)
                {
                    result[i][j] = array[j][i];
                }
            }

            return result;
        }

        private static int[][][] Transpose3(int[][][] array)
        {
            var result = new int[256][][];

            for (var x = 0; x < 256; x++)
            {
                result[x] = new int[256][];
                for (var y = 0; y < 256; y++)
                {
                    result[x][y] = new int[256];
                    for (var z = 0; z < 256; z++)
                    {
                        result[x][y][z] = array[z][y][x];
                    }
                }
            }

            return result;
        }

        private static int[][] Tile2(int[] array)
        {
            var result = new int[256][];

            for (var i = 0; i < 256; i++)
            {
                result[i] = array.ToArray();
            }

            return result;
        }

        private static int[][][] Tile3(int[][] array)
        {
            var result = new int[256][][];

            for (var i = 0; i < 256; i++)
            {
                result[i] = array.Clone() as int[][];
            }

            return result;
        }

        private static float[][] Divide(int[][] a, int[][] b)
        {
            var result = new float[256][];

            for (var i = 0; i < 256; i++)
            {
                result[i] = new float[256];

                for (var j = 0; j < 256; j++)
                {
                    var b1 = b[i][j];
                    if (b1 != 0) result[i][j] = (float)a[i][j] / b1;
                }
            }

            return result;
        }

        private static float[][][] Divide(int[][][] a, int[][][] b)
        {
            var result = new float[256][][];

            for (var i = 0; i < 256; i++)
            {
                result[i] = new float[256][];

                for (var j = 0; j < 256; j++)
                {
                    result[i][j] = new float[256];

                    for (var k = 0; k < 256; k++)
                    {
                        var b1 = b[i][j][k];
                        if (b1 != 0) result[i][j][k] = (float)a[i][j][k] / b1;
                    }
                }
            }

            return result;
        }

        private static float[][] Pow(float[][] a, float f)
        {
            var result = new float[256][];

            for (var i = 0; i < 256; i++)
            {
                result[i] = new float[256];

                for (var j = 0; j < 256; j++)
                {
                    result[i][j] = (float)Math.Pow(a[i][j], f);
                }
            }

            return result;
        }

        /// <summary>
        /// Global random number generator. Initialized once to keep the seed.
        /// </summary>
        private static readonly Random Rng = new Random();

        private static int Choice(int[] a, float[] p)
        {
            var roll = Rng.NextDouble();

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
        private static Color HsvToRgb(double h, double s, double v)
        {
            var H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
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
        private static int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }
    }
}
