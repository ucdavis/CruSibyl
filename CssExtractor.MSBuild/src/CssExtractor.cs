using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CssExtractor.MSBuild
{
    public static class CssExtractor
    {
        public static HashSet<string> ExtractCssClasses(IEnumerable<string> files, IEnumerable<string> patterns)
        {
            var result = new HashSet<string>();
            foreach (var file in files)
            {
                if (!File.Exists(file)) continue;
                var content = File.ReadAllText(file);
                foreach (var pattern in patterns)
                {
                    var matches = Regex.Matches(content, pattern);
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var classNames = match.Groups[1].Value.Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries);
                            foreach (var name in classNames)
                                result.Add(name.Trim());
                        }
                    }
                }
            }
            return result;
        }

        public static void WriteToFile(IEnumerable<string> cssClasses, string outputFile)
        {
            var outputDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            File.WriteAllLines(outputFile, cssClasses.OrderBy(c => c));
        }
    }
}