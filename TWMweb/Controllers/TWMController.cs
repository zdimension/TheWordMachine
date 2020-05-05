using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using libTWM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace TWMweb.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TWMController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<TWMController> _logger;

        public TWMController(ILogger<TWMController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TWMweb.index.html");
            using var reader = new StreamReader(stream);
            return Content(reader.ReadToEnd(), "text/html");
        }

        [HttpGet("/get")]
        public async Task<AnalysisResult> Get(int minSize, int maxSize, int numWords, bool excludeExisting)
        {
            var a = await Analyzer.BuildFromFileAsync("../data/FR.txt", log: _logger);

            return new AnalysisResult
            {
                MatrixSVG = a.ProbabilityImageSVG,
                Words = a.GenerateWords(minSize, maxSize, numWords, excludeExisting)
                    .Select(k => k.Value.ToArray()).ToArray()
            };
        }
    }
}
