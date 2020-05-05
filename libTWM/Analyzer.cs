using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SvgNet.SvgElements;
using SvgNet.SvgGdi;
using SvgNet.SvgTypes;

namespace libTWM
{
    public class Analyzer
    {
        public ILogger Logger { get; set; }
        public IRandomProvider RNG { get; set; } = new SystemRandomProvider();

        private List<char> _charMap;
        private int[][][] _count;

        private readonly MatrixMath _maths;

        private readonly Lazy<Image> _probabilityImage;
        private readonly Lazy<string> _probabilityImageSVG;
        private readonly Lazy<float[][]> _probabilityMatrix;
        private readonly Lazy<float[][][]> _probabilityMatrix3D;

        private Analyzer(IEnumerable<string> words, string name = "unknown", IProgress<float> progress = null,
            ILogger log = null)
        {
            Logger = log;
            Name = name;

            Logger?.LogTrace("Generating hashset");
            Words = words.Select(w => w.ToLower()).ToHashSet();

            _probabilityMatrix = new Lazy<float[][]>(ComputeProbabilityMatrix);
            _probabilityMatrix3D = new Lazy<float[][][]>(ComputeProbabilityMatrix3D);
            _probabilityImage = new Lazy<Image>(GenerateProbabilityImage);
            _probabilityImageSVG = new Lazy<string>(GenerateProbabilityImageSVG);

            LoadWords(progress);
            _maths = new MatrixMath(_charMap.Count);
        }

        public string Name { get; }

        public HashSet<string> Words { get; }

        public float[][] ProbabilityMatrix => _probabilityMatrix.Value;
        public float[][][] ProbabilityMatrix3D => _probabilityMatrix3D.Value;
        public Image ProbabilityImage => _probabilityImage.Value;
        public string ProbabilityImageSVG => _probabilityImageSVG.Value;
        public ReadOnlyCollection<char> CharacterMap => _charMap.AsReadOnly();

        public static async Task<Analyzer> BuildFromFileAsync(string filename, Encoding enc = null, IProgress<float> progress = null, ILogger log = null)
        {
            return await BuildFromListAsync(await File.ReadAllLinesAsync(filename, enc ?? Encoding.UTF8), filename, progress, log);
        }

        public static Analyzer BuildFromFile(string filename, Encoding enc = null, IProgress<float> progress = null, ILogger log = null)
        {
            return BuildFromList(File.ReadAllLines(filename, enc ?? Encoding.UTF8), filename, progress, log);
        }

        public static async Task<Analyzer> BuildFromListAsync(string[] words, string name = "unknown", IProgress<float> progress = null, ILogger log = null)
        {
            return await Task.Run(() => new Analyzer(words, name, progress, log));
        }

        public static Analyzer BuildFromList(string[] words, string name = "unknown", IProgress<float> progress = null, ILogger log = null)
        {
            return new Analyzer(words, name, progress, log);
        }

        private void WriteProbabilityImage(IGraphics gfx)
        {
            var proba2Da = ProbabilityMatrix;
            var bsize = _charMap.Count * 24 + 24;

            var font = new Font("Consolas", 20, FontStyle.Regular, GraphicsUnit.Pixel);
            var cm2 = _charMap.ToList();
            cm2.Sort();
            var format = new StringFormat { Alignment = StringAlignment.Center };

            gfx.CompositingQuality = CompositingQuality.HighQuality;
            gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
            gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
            gfx.SmoothingMode = SmoothingMode.HighQuality;
            gfx.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            gfx.Clear(Color.White);
            var ver = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
            gfx.DrawString($"twm {ver.Major}.{ver.Minor}", font, Brushes.Black, new RectangleF(0, 0, bsize, 24),
                format);
            gfx.DrawString("P", font, Brushes.Black, new RectangleF(0, 24, 24, 24), format);
            gfx.DrawString("=", font, Brushes.Black, new RectangleF(24, 24, 24, 24), format);
            gfx.DrawString("0", font, Brushes.Black, new RectangleF(48, 24, 24, 24), format);
            gfx.FillRectangle(Brushes.Black, 72, 24, 24, 24);
            for (var i = 0; i < bsize - 120; i++)
                gfx.FillRectangle(new SolidBrush(Utils.HsvToRgb((1 - i / (double)(bsize - 120)) * 240, 1, 1)), 96 + i,
                    24, 1, 24);

            gfx.DrawString("1", font, Brushes.Black, new RectangleF(bsize - 24, 24, 24, 24), format);
            for (var i = 0; i < _charMap.Count; i++)
            {
                gfx.DrawString((i == 0 ? '␂' : _charMap[i]).ToString(), font, new SolidBrush(Color.Black),
                    new RectangleF(0, 24 * (3 + cm2.IndexOf(_charMap[i])), 24, 24), format);
                gfx.DrawString((i == 0 ? '␃' : _charMap[i]).ToString(), font, new SolidBrush(Color.Black),
                    new RectangleF(24 * (1 + cm2.IndexOf(_charMap[i])), 48, 24, 24), format);

                for (var j = 0; j < _charMap.Count; j++)
                {
                    var prob = proba2Da[i][j];
                    var color = Color.Black;
                    // probability is explicitely set to zero iff it never happens
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if (prob != 0) color = Utils.HsvToRgb((1 - prob) * 240, 1, 1);
                    gfx.FillRectangle(new SolidBrush(color), 24 * (1 + cm2.IndexOf(_charMap[j])),
                        24 * (3 + cm2.IndexOf(_charMap[i])), 24, 24);
                }
            }
        }

        private Image GenerateProbabilityImage()
        {
            Logger?.LogDebug("Generating probability matrix (GDI)");

            var bsize = _charMap.Count * 24 + 24;
            var img = new Bitmap(bsize, bsize + 48);
            using var gfx = Graphics.FromImage(img);
            WriteProbabilityImage(new GdiGraphics(gfx));

            return img;
        }

        private string GenerateProbabilityImageSVG()
        {
            Logger?.LogDebug("Generating probability matrix (SVG)");

            var bsize = _charMap.Count * 24 + 24;
            var gfx = new SvgGraphics();
            var root = (SvgSvgElement)(typeof(SvgGraphics)
                .GetField("_root", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(gfx));
            root.Width = bsize;
            root.Height = bsize + 48;
            root.ViewBox = new SvgNumList(new[] {0f, 0f, root.Width.Value, root.Height.Value});
            WriteProbabilityImage(gfx);

            return root.WriteSVGString(false);
        }

        private float[][][] ComputeProbabilityMatrix3D()
        {
            var sum = _maths.SumAxis2(_count);
            var sumTiled = _maths.Transpose3(_maths.Tile3(_maths.Transpose2(sum)));
            return _maths.Divide(_count, sumTiled);
        }

        private float[][] ComputeProbabilityMatrix()
        {
            var count2D = _maths.SumAxis0(_count);
            var proba2D = _maths.Divide(count2D,
                _maths.Transpose2(_maths.Tile2(_maths.Sum(_maths.Transpose2(count2D)))));
            const float alpha = 0.33f;
            return _maths.Pow(proba2D, alpha);
        }

        private void LoadWords(IProgress<float> progress=null)
        {
            /*
            [0] = \0 (word start)
            [2] = A
            [3] = B
            */
            _charMap = new List<char> {'\0'};

            foreach (var c in from w in Words from c in w where !_charMap.Contains(c) select c) _charMap.Add(c);

            var matSize = _charMap.Count;

            _count = new int[matSize][][];

            Parallel.For(0, matSize, i =>
            {
                _count[i] = new int[matSize][];
                Parallel.For(0, matSize, j => { _count[i][j] = new int[matSize]; });
            });

            var cnt = 0;
            var step = Words.Count / 1000 + 1;
            foreach (var l2 in Words)
            {
                var l = l2;
                var i = '\0';
                var j = '\0';
                foreach (var k in l.ToLower() + "\0")
                {
                    _count[_charMap.IndexOf(i)][_charMap.IndexOf(j)][_charMap.IndexOf(k)]++;
                    i = j;
                    j = k;
                }

                if (cnt % step == 0)
                {
                    progress?.Report((float)cnt / Words.Count);
                }

                cnt++;
            }

            progress?.Report(1);
        }

        public Dictionary<int, HashSet<string>> GenerateWords(int minSize,
            int maxSize, int numWords,
            bool excludeExisting,
            IProgress<(int, int)> progress=null)
        {
            if (minSize < 0)
                throw new ArgumentOutOfRangeException(nameof(minSize), "minSize must be greater than zero");

            if (maxSize < minSize)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "maxSize must be greater than or equal to minSize");

            if (Math.Pow(numWords, 1d / minSize) > _charMap.Count - 1)
                throw new ArgumentOutOfRangeException(nameof(numWords), "numWords must be less than or equal to numChars ^ minSize");

            Logger?.LogDebug("Generating words");
            var proba = ProbabilityMatrix3D;
            var numSizes = maxSize - minSize + 1;
            var genWords = new HashSet<string>[numSizes];
            for (var i = 0; i < numSizes; i++) genWords[i] = new HashSet<string>();

            var curWord = new StringBuilder();
            var choices = Enumerable.Range(0, _charMap.Count).ToArray();
            for (var numRemaining = numSizes; numRemaining > 0;)
            {
                var i = 0;
                var j = 0;
                int k;

                while (true)
                {
                    k = Utils.Choice(choices, proba[i][j], RNG);
                    if (k == 0) break;
                    if (curWord.Length >= maxSize) break;
                    curWord.Append(_charMap[k]);
                    i = j;
                    j = k;
                }

                if (k == 0 && curWord.Length >= minSize)
                {
                    var x = curWord.ToString();
                    var size = curWord.Length - minSize;

                    if (genWords[size].Count == numWords)
                        continue;

                    if (genWords[size].Contains(x))
                        continue;

                    if (Words.Contains(x))
                        if (excludeExisting) continue;
                        else x += "*";

                    genWords[size].Add(x);

                    if (genWords[size].Count == numWords)
                        numRemaining--;

                    progress?.Report((size, genWords[size].Count));
                }

                curWord.Clear();
            }

            progress?.Report((0, numWords));

            return genWords
                .Select((h, i) => (h, i))
                .ToDictionary(
                    t => minSize + t.i,
                    t => t.h);
        }
    }
}