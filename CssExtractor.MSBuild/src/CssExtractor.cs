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
                
                ExtractFromClassAttributes(content, result);
                ExtractFromCustomPatterns(content, patterns, result);
            }
            return result;
        }
        
        private static void ExtractFromClassAttributes(string content, HashSet<string> result)
        {
            // Handle both double and single quoted class attributes
            ExtractQuotedClassAttributes(content, result, '"');
            ExtractQuotedClassAttributes(content, result, '\'');
        }
        
        private static void ExtractQuotedClassAttributes(string content, HashSet<string> result, char quoteChar)
        {
            var pattern = $@"class\s*=\s*{Regex.Escape(quoteChar.ToString())}";
            var matches = Regex.Matches(content, pattern);
            
            foreach (Match match in matches)
            {
                var startIndex = match.Index + match.Length;
                var classValue = ExtractQuotedAttributeValue(content, startIndex, quoteChar);
                if (!string.IsNullOrEmpty(classValue))
                {
                    TokenizeAndExtractClasses(classValue, result);
                }
            }
        }
        
        private static string ExtractQuotedAttributeValue(string content, int startIndex, char quoteChar)
        {
            var result = new System.Text.StringBuilder();
            var i = startIndex;
            var depth = 0;
            
            while (i < content.Length)
            {
                var c = content[i];
                
                if (c == quoteChar && depth == 0)
                {
                    // Found the closing quote
                    break;
                }
                else if (c == '(' && i > 0 && content[i - 1] == '@')
                {
                    // Entering a Razor interpolation
                    depth++;
                    result.Append(c);
                }
                else if (c == ')' && depth > 0)
                {
                    // Exiting a Razor interpolation
                    depth--;
                    result.Append(c);
                }
                else
                {
                    result.Append(c);
                }
                
                i++;
            }
            
            return result.ToString();
        }
        
        private static void TokenizeAndExtractClasses(string classValue, HashSet<string> result)
        {
            // Tokenize everything in the class attribute value
            // Split by common delimiters and extract potential class names
            var tokens = Regex.Split(classValue, @"[\s\t\n@(){}?:;,""']+")
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Select(token => token.Trim())
                .Where(token => !string.IsNullOrEmpty(token));
            
            foreach (var token in tokens)
            {
                if (IsLikelyClass(token))
                {
                    result.Add(token);
                }
            }
        }
        
        private static bool IsLikelyClass(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            
            // Filter out obvious non-classes
            if (token.Contains("=") || token.Contains("!") || token.Contains("*") || token.Contains("/"))
                return false;
            
            // Filter out pure numbers and very long tokens
            if (token.All(char.IsDigit) || token.Length > 50) 
                return false;
            
            // Basic CSS class name validation - start with letter, underscore, or hyphen
            return Regex.IsMatch(token, @"^[a-zA-Z_-][\w-]*$");
        }

        private static void ExtractFromCustomPatterns(string content, IEnumerable<string> patterns, HashSet<string> result)
        {
            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                
                try
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    var matches = regex.Matches(content);
                    
                    foreach (Match match in matches)
                    {
                        // Extract the first capture group (which should contain the class name)
                        if (match.Groups.Count > 1)
                        {
                            var className = match.Groups[1].Value;
                            if (!string.IsNullOrWhiteSpace(className))
                            {
                                // Split by whitespace in case multiple classes are in one match
                                var classNames = className.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var cls in classNames)
                                {
                                    var trimmedClass = cls.Trim();
                                    if (IsLikelyClass(trimmedClass))
                                    {
                                        result.Add(trimmedClass);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log regex errors but continue processing
                    System.Diagnostics.Debug.WriteLine($"Error processing pattern '{pattern}': {ex.Message}");
                }
            }
        }

        public static void WriteToFile(IEnumerable<string> cssClasses, string outputFile)
        {
            var sortedClasses = cssClasses.OrderBy(c => c).ToArray();
            var newContent = string.Join(Environment.NewLine, sortedClasses);
            
            // Only write if content has changed
            bool shouldWrite = true;
            if (File.Exists(outputFile))
            {
                var existingContent = File.ReadAllText(outputFile);
                shouldWrite = existingContent != newContent;
            }
            
            if (shouldWrite)
            {
                var outputDir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                File.WriteAllText(outputFile, newContent);
            }
        }
    }
}