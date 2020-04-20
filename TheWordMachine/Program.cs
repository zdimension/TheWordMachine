using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable SuggestBaseTypeForParameter

namespace TheWordMachine
{
    public static class Program
    {
        private static int MinWordSize { get; set; } = 3;
        private static int MaxWordSize { get; set; } = 12;
        private static int WordsPerSize { get; set; } = 100;
        private static bool ExcludeExistingWords { get; set; }
        private static Encoding TheEncoding { get; set; } = Encoding.UTF8;
        private static bool UseREPL { get; set; } = false;

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
                    "\t-repl\t--show-similarity\tDisplay the percentage of similarity of a word\n" +
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
                    case "-repl":
                    case "--show-similarity":
                        UseREPL = true;
                        continue;
                }
                if (sp.Length > 1)
                {
                    switch (sp[0])
                    {
                        case "-enc":
                        case "--encoding":
                            try
                            {
                                TheEncoding = Encoding.GetEncoding(sp[1]);
                            }
                            catch
                            {
                                Console.WriteLine($"Invalid encoding value: {sp[1]}. Using default ({TheEncoding.EncodingName}");
                            }
                            continue;
                    }

                    if (int.TryParse(sp[1], out var v))
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
                    else Console.WriteLine($"Invalid number: {sp[2]}");
                }
                else Console.WriteLine("Expected value");
            }

            if (UseREPL)
            {
                var objs = inps
                    .Select(LoadWords)
                    .Select(o => (o.charMap, GenerateProbMatrix(o.count)))
                    .ToList();

                while (true)
                {
                    Console.WriteLine("Type ; to exit");
                    Console.Write("> ");

                    var mot = Console.ReadLine().Trim();
                    if (mot == ";")
                        break;

                    var proba = new List<double>();

                    foreach (var (charMap, probm) in objs)
                    {
                        var p = 1f;
                        var last = 0;

                        foreach (var k in (mot.ToLower() + "\0"))
                        {
                            try
                            {
                                var idx = charMap.IndexOf(k);
                                var cur = probm[last][idx];
                                p *= cur;
                                last = idx;
                            }
                            catch
                            {
                                // ignored
                            }
                        }

                        //proba.Add(p / (/*mot.Length +*/ 1));
                        proba.Add(Math.Pow(p, 1d / (mot.Length + 1)));
                    }

                    Console.WriteLine("Score:");

                    var maxp = proba.Max();

                    foreach (var (inp, prob) in inps.Zip(proba, Tuple.Create))
                    {
                        Console.WriteLine($" {inp} : {prob:P} {(inps.Length > 1 && prob == maxp ? "<-- most likely" : "")}");
                    }
                }
            }
            else
            {
                foreach (var s in inps)
                {
                    GenMots(s);
                }
            }
        }

        private static int matSize;

        private static void GenMots(string n)
        {
            Console.WriteLine($"Generating words for file: {n}");
            var outp = Path.Combine(Environment.CurrentDirectory, "output", n);
            if (Directory.Exists(outp))
            {
                Directory.Delete(outp, true);
            }
            for (var i = 0; i < 5; i++)
            {
                Directory.CreateDirectory(outp);
                if (Directory.Exists(outp))
                    break;
            }

            var (lines, count, charMap) = LoadWords(n);

            GenerateProbImage(outp, charMap, count);

            GenerateWords(outp, charMap, count, lines);
        }

        private static (string[] lines, int[][][] count, List<char> charMap) LoadWords(string fn)
        {
            var count = new int[256][][];
            var charMap = new List<char> { '\0' };
            /*
            [0] = \0 (word start)
            [2] = A
            [3] = B
            */
            Parallel.For(0, 256, i =>
            {
                count[i] = new int[256][];
                Parallel.For(0, 256, j =>
                {
                    count[i][j] = new int[256];
                });
            });

            if (!File.Exists(fn))
            {
                fn = Path.Combine("data", fn);
            }

            if (!File.Exists(fn))
            {
                Console.WriteLine($"File not found: {fn} ; skipping");
                return default;
            }

            var lines = File.ReadAllLines(fn, TheEncoding);
            var cnt = 0;
            var countLen = lines.Length.ToString().Length;
            foreach (var l2 in lines)
            {
                var l = l2;
                var i = '\0';
                var j = '\0';
                foreach (var k in (l.ToLower() + "\0"))
                {
                    if (!charMap.Contains(k)) charMap.Add(k);
                    count[charMap.IndexOf(i)][charMap.IndexOf(j)][charMap.IndexOf(k)]++;
                    i = j;
                    j = k;
                }

                if (cnt % 1234 == 0)
                {
                    Console.Write($"{fn} {cnt.ToString().PadLeft(countLen)} / {lines.Length}");
                    Console.CursorLeft = 0;
                }

                cnt++;
            }

            Console.WriteLine($"{fn} {lines.Length} / {lines.Length}");

            matSize = charMap.Count;

            var newCount = new int[matSize][][];

            Parallel.For(0, matSize, i =>
            {
                newCount[i] = new int[matSize][];
                Parallel.For(0, matSize, j =>
                {
                    newCount[i][j] = new int[matSize];
                    Array.Copy(count[i][j], newCount[i][j], matSize);
                });
            });

            return (lines, newCount, charMap);
        }

        private static float[][] GenerateProbMatrix(int[][][] count)
        {
            var count2D = SumAxis0(count);
            var proba2D = Divide(count2D, Transpose2(Tile2(Sum(Transpose2(count2D)))));
            var alpha = 0.33f;
            return Pow(proba2D, alpha);
        }

        private static void GenerateProbImage(string outp, List<char> charMap, int[][][] count)
        {
            Console.WriteLine("Generating probability matrix");

            var proba2Da = GenerateProbMatrix(count);
            var charMapFiltered = charMap
                .Where(x => !char.IsDigit(x))
                .ToList();
            var cskip = 0;
            var bsize = (charMapFiltered.Count - cskip) * 24 + 24;
            var img = new Bitmap(bsize, bsize + 48);
            var font = new Font("Consolas", 20, FontStyle.Regular, GraphicsUnit.Pixel);
#if TRIER_ACCENTS
            var cm2_1 = charMapFiltered.Select(x => ((char)x).ToString()).ToList();
            cm2_1.Sort();
            var cm2 = cm2_1.Select(x => (int) x[0]).ToList();
#else
            var cm2 = charMapFiltered.ToList();
            cm2.Sort();
#endif
            var format = new StringFormat {Alignment = StringAlignment.Center};
            using (var gfx = Graphics.FromImage(img))
            {
                gfx.CompositingQuality = CompositingQuality.HighQuality;
                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
                gfx.SmoothingMode = SmoothingMode.HighQuality;
                gfx.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                gfx.Clear(Color.White);
                var ver = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
                gfx.DrawString($"twm {ver.Major}.{ver.Minor}", font, Brushes.Black, new RectangleF(0, 0, bsize, 24), format);
                gfx.DrawString("P", font, Brushes.Black, new RectangleF(0, 24, 24, 24), format);
                gfx.DrawString("=", font, Brushes.Black, new RectangleF(24, 24, 24, 24), format);
                gfx.DrawString("0", font, Brushes.Black, new RectangleF(48, 24, 24, 24), format);
                gfx.FillRectangle(Brushes.Black, 72, 24, 24, 24);
                for (var i = 0; i < bsize - 120; i++)
                {
                    gfx.FillRectangle(new SolidBrush(HsvToRgb((1 - i / (double)(bsize - 120)) * 240, 1, 1)), 96 + i, 24, 1, 24);
                }
                gfx.DrawString("1", font, Brushes.Black, new RectangleF(bsize - 24, 24, 24, 24), format);
                for (var i = cskip; i < charMapFiltered.Count; i++)
                {
                    gfx.DrawString((i == 0 ? '␂' : charMapFiltered[i]).ToString(), font, new SolidBrush(Color.Black), new RectangleF(0, 24 * (3 + cm2.IndexOf(charMapFiltered[i]) - cskip), 24, 24), format);
                    gfx.DrawString((i == 0 ? '␃' : charMapFiltered[i]).ToString(), font, new SolidBrush(Color.Black), new RectangleF(24 * (1 + cm2.IndexOf(charMapFiltered[i]) - cskip), 48, 24, 24), format);

                    for (var j = cskip; j < charMapFiltered.Count; j++)
                    {
                        var prob = proba2Da[i][j];
                        var color = Color.Black;
                        // probability is explicitely set to zero iff it never happens
                        // ReSharper disable once CompareOfFloatsByEqualityOperator
                        if (prob != 0)
                            color = HsvToRgb((1 - prob) * 240, 1, 1);
                        gfx.FillRectangle(new SolidBrush(color), 24 * (1 + cm2.IndexOf(charMapFiltered[j]) - cskip), 24 * (3 + cm2.IndexOf(charMapFiltered[i]) - cskip), 24, 24);
                    }
                }
            }

            int att;
            for (att = 0; att < 5; att++)
            {
                try
                {
                    img.Save(Path.Combine(outp, "matrix.png"), ImageFormat.Png);
                    break;
                }
                catch
                {
                    // ignored
                }
            }

            if (att == 5)
            {
                Console.WriteLine("Failed to generating probability matrix");
            }
        }

        private static void GenerateWords(string outp, List<char> charMap, int[][][] count, string[] linesList)
        {
            Console.WriteLine("Generating lines hashset");
            var lines = new HashSet<string>(linesList);
            Console.WriteLine("Generating words");
            var sum = SumAxis2(count);
            var sumTiled = Transpose3(Tile3(Transpose2(sum)));
            var proba = Divide(count, sumTiled);
            var numSizes = MaxWordSize - MinWordSize + 1;
            var genWords = new HashSet<string>[numSizes];
            for (var i = 0; i < numSizes; i++)
            {
                genWords[i] = new HashSet<string>();
            }

            var countLen = WordsPerSize.ToString().Length;
            var curWord = new StringBuilder();
            var choices = Enumerable.Range(0, matSize).ToArray();
            for (var numRemaining = numSizes; numRemaining > 0;)
            {
                var i = 0;
                var j = 0;
                int k;

                while (true)
                {
                    k = Choice(choices, proba[i][j]);
                    if (k == 0) break;
                    if (curWord.Length >= MaxWordSize) break;
                    curWord.Append(charMap[k]);
                    i = j;
                    j = k;
                }

                if (k == 0 && curWord.Length >= MinWordSize)
                {
                    var x = curWord.ToString();
                    var size = curWord.Length - MinWordSize;

                    if (genWords[size].Count == WordsPerSize)
                        continue;

                    if (genWords[size].Contains(x))
                        continue;

                    if (lines.Contains(x))
                        if (ExcludeExistingWords) continue;
                        else x += "*";

                    genWords[size].Add(x);

                    if (genWords[size].Count == WordsPerSize)
                        numRemaining--;

                    Console.Write(string.Join(" / ", genWords.Select(c => c.Count.ToString().PadLeft(countLen))));
                    Console.CursorLeft = 0;
                }

                curWord.Clear();
            }

            Console.WriteLine("\nDone");

            for (var size = MinWordSize; size <= MaxWordSize; size++)
            {
                File.WriteAllLines(Path.Combine(outp, $"words_{size}.txt"), genWords[size - MinWordSize]);
            }
        }

        private static int[] Sum(int[][] arr)
        {
            var result = new int[matSize];

            Parallel.For(0, matSize, i =>
            {
                var s = 0;

                for (var j = 0; j < matSize; j++)
                {
                    s += arr[j][i];
                }

                result[i] = s;
            });

            return result;
        }

        private static int[][] SumAxis0(int[][][] arr)
        {
            var result = new int[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new int[matSize];

                Parallel.For(0, matSize, j =>
                {
                    var s = 0;

                    for (var k = 0; k < matSize; k++)
                    {
                        s += arr[k][i][j];
                    }

                    result[i][j] = s;
                });
            });

            return result;
        }

        private static int[][] SumAxis2(int[][][] arr)
        {
            var result = new int[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new int[matSize];

                Parallel.For(0, matSize, j =>
                {
                    var s = 0;

                    for (var k = 0; k < matSize; k++)
                    {
                        s += arr[i][j][k];
                    }

                    result[i][j] = s;
                });
            });

            return result;
        }

        private static int[][] Transpose2(int[][] array)
        {
            var result = new int[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new int[matSize];

                Parallel.For(0, matSize, j =>
                {
                    result[i][j] = array[j][i];
                });
            });

            return result;
        }

        private static int[][][] Transpose3(int[][][] array)
        {
            var result = new int[matSize][][];

            Parallel.For(0, matSize, x =>
            {
                result[x] = new int[matSize][];

                Parallel.For(0, matSize, y =>
                {
                    result[x][y] = new int[matSize];

                    Parallel.For(0, matSize, z =>
                    {
                        result[x][y][z] = array[z][y][x];
                    });
                });
            });

            return result;
        }

        private static int[][] Tile2(int[] array)
        {
            var result = new int[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = array.ToArray();
            });

            return result;
        }

        private static int[][][] Tile3(int[][] array)
        {
            var result = new int[matSize][][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = array.Select(a => a.ToArray()).ToArray();
            });

            return result;
        }

        private static float[][] Divide(int[][] a, int[][] b)
        {
            var result = new float[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new float[matSize];

                Parallel.For(0, matSize, j =>
                {
                    var b1 = b[i][j];
                    if (b1 != 0) result[i][j] = (float)a[i][j] / b1;
                });
            });

            return result;
        }

        private static float[][][] Divide(int[][][] a, int[][][] b)
        {
            var result = new float[matSize][][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new float[matSize][];

                Parallel.For(0, matSize, j =>
                {
                    result[i][j] = new float[matSize];

                    Parallel.For(0, matSize, k =>
                    {
                        var b1 = b[i][j][k];
                        if (b1 != 0) result[i][j][k] = (float)a[i][j][k] / b1;
                    });
                });
            });

            return result;
        }

        private static float[][] Pow(float[][] a, float f)
        {
            var result = new float[matSize][];

            Parallel.For(0, matSize, i =>
            {
                result[i] = new float[matSize];

                Parallel.For(0, matSize, j =>
                {
                    result[i][j] = (float)Math.Pow(a[i][j], f);
                });
            });

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
        private static int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }
    }
}
