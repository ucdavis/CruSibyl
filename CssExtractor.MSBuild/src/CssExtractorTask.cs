using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace CssExtractor.MSBuild
{
    public class CssExtractorTask : Microsoft.Build.Utilities.Task
    {
        public ITaskItem[] CssExtractorIncludeFiles { get; set; } = [];
        public string CssExtractorOutputFile { get; set; } = "extracted-classes.txt";
        public ITaskItem[] CssExtractorExcludeFiles { get; set; } = [];
        public string CssExtractorPatterns { get; set; } = "";

        public override bool Execute()
        {
            try
            {
                // Split patterns on ';', trim, and ignore empty
                var extractionPatterns = (CssExtractorPatterns ?? "")
                    .Split(';')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();

                var excludeFileGlobs = CssExtractorExcludeFiles.Select(x => x.ItemSpec).ToArray() ?? [];

                var excludeFiles = new HashSet<string>(
                    CssExtractorExcludeFiles.Select(x => Path.GetFullPath(x.ItemSpec)) ?? [],
                    StringComparer.OrdinalIgnoreCase);

                var filesToProcess = new List<string>();

                if (CssExtractorIncludeFiles != null)
                    filesToProcess.AddRange(CssExtractorIncludeFiles.Select(f => f.ItemSpec));

                Log.LogMessage(MessageImportance.High, $"Processing {filesToProcess.Count} files with {extractionPatterns.Length} patterns");
                Log.LogMessage(MessageImportance.High, $"Patterns: {string.Join("; ", extractionPatterns)}");
                
                foreach (var file in filesToProcess.Take(5)) // Log first 5 files as sample
                {
                    Log.LogMessage(MessageImportance.High, $"File: {file}");
                }

                var cssClasses = CssExtractor.ExtractCssClasses(filesToProcess.Distinct(), extractionPatterns);
                CssExtractor.WriteToFile(cssClasses, CssExtractorOutputFile.Trim());

                Log.LogMessage(MessageImportance.High, $"Extracted {cssClasses.Count} CSS classes to {CssExtractorOutputFile}");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"CSS extraction failed: {ex.Message}");
                return false;
            }
        }
    }
}