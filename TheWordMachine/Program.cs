using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using libTWM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

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
        private static bool UseREPL { get; set; }

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
            var inps = args.Where(x => x[0] != '-').Select(x =>
            {
                if (!File.Exists(x))
                {
                    x = Path.Combine("data", x);
                }

                if (!File.Exists(x))
                {
                    return null;
                }

                return x;
            }).Where(x => x != null).ToArray();

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
                var objs = inps.Select(f => Task.Run(() =>
                {
                    var res = Analyzer.BuildFromFile(f);
                    Console.WriteLine($"Done {f}");
                    return res;
                })).ToArray();
                Task.WaitAll(objs);
                
                while (true)
                {
                    Console.WriteLine("Type ; to exit");
                    Console.Write("> ");

                    var mot = Console.ReadLine().Trim();
                    if (mot == ";")
                        break;

                    var proba = new List<double>();

                    foreach (var analyzer in objs.Select(t => t.Result))
                    {
                        var p = 1f;
                        var last = 0;

                        foreach (var k in (mot.ToLower() + "\0"))
                        {
                            try
                            {
                                var idx = analyzer.CharacterMap.IndexOf(k);
                                var cur = analyzer.ProbabilityMatrix[last][idx];
                                p *= cur;
                                last = idx;
                            }
                            catch
                            {
                                // ignored
                            }
                        }

                        proba.Add(Math.Pow(p, 1d / (mot.Length + 1)));
                    }

                    Console.WriteLine("Score:");

                    var maxp = proba.Max();
                    var ml = inps.Max(l => l.Length);

                    foreach (var (inp, prob) in inps.Zip(proba, Tuple.Create))
                    {
                        Console.WriteLine($" {inp.PadRight(ml)} : {prob,8:P} {(inps.Length > 1 && prob.Equals(maxp) ? "<-- most likely" : "")}");
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
            var outp = Path.Combine(Environment.CurrentDirectory, "output", Path.GetFileName(n));
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

            try
            {
                var a = Analyzer.BuildFromFile(n, progress: new Progress<float>(p =>
                {
                    Console.Write($"Analysis {p,5:P0}\r");
                }), log: new BasicConsoleLogger());
                Console.WriteLine("Loading complete.");

                a.ProbabilityImage.Save(Path.Combine(outp, "matrix.png"), ImageFormat.Png);
                File.WriteAllText(Path.Combine(outp, "matrix.svg"), a.ProbabilityImageSVG);

                var progress = new int[MaxWordSize - MinWordSize + 1];
                var countLen = WordsPerSize.ToString().Length;
                var prog = new Progress<(int, int)>(t =>
                {
                    progress[t.Item1] = t.Item2;
                });

                var res = Task.Run(() => a.GenerateWords(MinWordSize, MaxWordSize, WordsPerSize,
                    ExcludeExistingWords, prog));
                do
                {
                    Thread.Sleep(100);
                    Console.Write(string.Join(" / ", progress.Select(c => c.ToString().PadLeft(countLen))) + "\r");
                } while (!res.IsCompleted);
                Console.WriteLine();
                foreach (var (size, words) in res.Result)
                {
                    File.WriteAllLines(Path.Combine(outp, $"words_{size}.txt"), words);
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                Console.WriteLine($"File not found {n} ; skipping");
            }
        }
    }
}
