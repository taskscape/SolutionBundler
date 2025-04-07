using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SolutionProcessor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: SolutionProcessor <path-to-solution-file> [output-file]");
                Console.WriteLine("If output-file is not specified, output.txt will be used in the current directory");
                return;
            }

            string solutionPath = args[0];
            string outputPath = args.Length > 1 ? args[1] : "output.txt";

            if (!File.Exists(solutionPath))
            {
                Console.WriteLine($"Error: Solution file not found at {solutionPath}");
                return;
            }

            try
            {
                Console.WriteLine($"Processing solution: {solutionPath}");
                var processor = new SolutionProcessor(solutionPath);
                await processor.ProcessSolutionAsync(outputPath);
                Console.WriteLine($"Processing complete. Output written to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing solution: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    public class SolutionProcessor
    {
        private readonly string _solutionPath;
        private readonly string _solutionDirectory;
        private readonly Dictionary<string, string> _processedFiles = new();
        private readonly HashSet<string> _projectExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".csproj", ".vbproj", ".fsproj", ".sqlproj", ".dbproj", ".ccproj"
        };
        private readonly HashSet<string> _textFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".vb", ".fs", ".sql", ".xml", ".json", ".md", ".txt", ".config", ".settings",
            ".xaml", ".cshtml", ".html", ".css", ".js", ".ts", ".razor", ".resx", ".yml", ".yaml",
            ".gitignore", ".editorconfig", ".props", ".targets", ".manifest", ".asax", ".ashx",
            ".aspx", ".sln", ".csproj", ".vbproj", ".fsproj", ".sqlproj", ".dbproj", ".ccproj"
        };
        
        private readonly HashSet<string> _binaryFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".tiff", ".webp", ".svg",
            ".dll", ".exe", ".pdb", ".zip", ".tar", ".gz", ".7z", ".rar",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".mp3", ".mp4", ".wav", ".avi", ".mov", ".wmv", ".flv"
        };
        private readonly HashSet<string> _excludedDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", "node_modules", ".vs", ".git", "packages"
        };

        public SolutionProcessor(string solutionPath)
        {
            _solutionPath = Path.GetFullPath(solutionPath);
            _solutionDirectory = Path.GetDirectoryName(_solutionPath) ?? string.Empty;
        }

        public async Task ProcessSolutionAsync(string outputPath)
        {
            _processedFiles.Clear();
            
            // First process the solution file itself
            await ProcessFileAsync(_solutionPath);
            
            // Get all project files from the solution
            var projectFiles = ParseSolutionForProjects(_solutionPath);
            Console.WriteLine($"Found {projectFiles.Count} project(s) in solution");

            // Process each project file
            foreach (var projectFile in projectFiles)
            {
                _processedFiles.Add(":SPACER-" + projectFile, projectFile);

                string fullPath = Path.GetFullPath(Path.Combine(_solutionDirectory, projectFile));
                if (File.Exists(fullPath))
                {
                    await ProcessProjectAsync(fullPath);
                }
                else
                {
                    Console.WriteLine($"Warning: Project file not found at {fullPath}");
                }
            }

            // Write all processed content to the output file
            await WriteOutputAsync(outputPath);
        }

        private List<string> ParseSolutionForProjects(string solutionPath)
        {
            var projectPaths = new List<string>();
            var solutionLines = File.ReadAllLines(solutionPath);

            foreach (var line in solutionLines)
            {
                // Project entry format in .sln file:
                // Project("{GUID}") = "ProjectName", "RelativePath\To\Project.csproj", "{ProjectGUID}"
                if (line.StartsWith("Project("))
                {
                    var parts = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var projectPathPart = parts[1].Trim();
                        
                        // Remove quotes around the path
                        if (projectPathPart.StartsWith("\"") && projectPathPart.EndsWith("\""))
                        {
                            projectPathPart = projectPathPart.Substring(1, projectPathPart.Length - 2);
                        }
                        
                        // Only add if it has a recognized project extension
                        if (_projectExtensions.Contains(Path.GetExtension(projectPathPart)))
                        {
                            projectPaths.Add(projectPathPart);
                        }
                    }
                }
            }

            return projectPaths;
        }

        private async Task ProcessProjectAsync(string projectPath)
        {
            Console.WriteLine($"Processing project: {projectPath}");
    
            await ProcessFileAsync(projectPath);
    
            string projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
            var includedFiles = ParseProjectForFiles(projectPath);
    
            foreach (var file in includedFiles)
            {
                string fullPath = Path.GetFullPath(Path.Combine(projectDir, file));
                if (File.Exists(fullPath))
                {
                    string ext = Path.GetExtension(fullPath).ToLower();
                    if (_textFileExtensions.Contains(ext))
                    {
                        await ProcessFileAsync(fullPath);
                    }
                    else if (_binaryFileExtensions.Contains(ext))
                    {
                        await ProcessBinaryFileAsync(fullPath);
                    }
                }
            }
    
            await ProcessDirectoryAsync(projectDir);
        }

        private List<string> ParseProjectForFiles(string projectPath)
        {
            var includedFiles = new List<string>();
            
            try
            {
                var projectXml = XDocument.Load(projectPath);
                var xmlns = projectXml.Root?.GetDefaultNamespace();

                // Look for Include attributes on common item nodes
                var itemGroups = projectXml.Root?.Elements()
                    .Where(e => e.Name.LocalName == "ItemGroup")
                    .ToList();

                if (itemGroups != null)
                {
                    foreach (var itemGroup in itemGroups)
                    {
                        var items = itemGroup.Elements()
                            .Where(e => e.Attribute("Include") != null)
                            .Select(e => e.Attribute("Include")?.Value)
                            .Where(v => !string.IsNullOrEmpty(v))
                            .ToList();
                        
                        includedFiles.AddRange(items.Where(i => i != null).Select(i => i.Replace('\\', Path.DirectorySeparatorChar)));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error parsing project file {projectPath}: {ex.Message}");
            }
            
            return includedFiles;
        }

        private async Task ProcessDirectoryAsync(string directoryPath)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(directoryPath))
                {
                    // Skip excluded directories
                    if (_excludedDirectories.Contains(Path.GetFileName(dir)))
                    {
                        continue;
                    }
                    
                    await ProcessDirectoryAsync(dir);
                }
                
                foreach (string file in Directory.GetFiles(directoryPath))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    
                    if (_textFileExtensions.Contains(ext))
                    {
                        await ProcessFileAsync(file);
                    }
                    else if (_binaryFileExtensions.Contains(ext))
                    {
                        // Process binary file metadata without reading content
                        await ProcessBinaryFileAsync(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error processing directory {directoryPath}: {ex.Message}");
            }
        }

        private async Task ProcessFileAsync(string filePath)
        {
           
            string normalizedPath = Path.GetFullPath(filePath).ToLower();

            // Skip if it's a designer file
            if (normalizedPath.Contains(".designer.cs"))
            {
                return;
            }
            
            // Skip if it's a resource file
            if (normalizedPath.Contains(".resx") || normalizedPath.Contains("locale_"))
            {
                return;
            }
            
            // Skip if it's a test file
            if (normalizedPath.Contains("test"))
            {
                return;
            }

            // Skip if it's a test file
            if (normalizedPath.Contains("logs\\") || normalizedPath.Contains(".log") || normalizedPath.Contains(".log.txt"))
            {
                return;
            }

            // Skip if it's a bundle file
            if (normalizedPath.Contains("bundle") || normalizedPath.Contains(".min"))
            {
                return;
            }

            // Skip if it's a library file
            if (normalizedPath.Contains("jquery") || normalizedPath.Contains("bootstrap") || normalizedPath.Contains("signalr.js") || normalizedPath.Contains("charts.js") || normalizedPath.Contains("quill.js") || normalizedPath.Contains("aspxscriptintellisense") || normalizedPath.Contains("microsoftajax") || normalizedPath.Contains("microsoftmvcajax") || normalizedPath.Contains("microsoftmvcvalidation") || normalizedPath.Contains("explorercanvas.js") || normalizedPath.Contains("guiders.js") || normalizedPath.Contains("moment.js") ||normalizedPath.Contains("dhtmlxgantt") )
            {
                return;
            }
            
            // Skip if it's a stylesheet file
            if (normalizedPath.Contains(".css"))
            {
                return;
            }

            // Skip if it's a licence file
            if (normalizedPath.Contains("licence.txt"))
            {
                return;
            }

            
            // Skip if we already processed this file
            if (_processedFiles.ContainsKey(normalizedPath))
            {
                return;
            }
            
            try
            {
                string content = await File.ReadAllTextAsync(normalizedPath);
                
                // Record this file as processed
                string relativePath = Path.GetRelativePath(_solutionDirectory, normalizedPath);
                _processedFiles[normalizedPath] = content;
                
                Console.WriteLine($"Processed: {relativePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error reading file {filePath}: {ex.Message}");
            }
        }
        
        private async Task ProcessBinaryFileAsync(string filePath)
        {
            // Skip if we already processed this file
            string normalizedPath = Path.GetFullPath(filePath);
            if (_processedFiles.ContainsKey(normalizedPath))
            {
                return;
            }
            
            try
            {
                // Get file info
                var fileInfo = new FileInfo(normalizedPath);
                string relativePath = Path.GetRelativePath(_solutionDirectory, normalizedPath);
                
                // Create a metadata entry instead of file content
                string metadataContent = $"[Binary File: {Path.GetFileName(normalizedPath)}]\n" +
                                        $"Size: {FormatFileSize(fileInfo.Length)}\n" +
                                        $"Last Modified: {fileInfo.LastWriteTime}\n" +
                                        $"Type: {Path.GetExtension(normalizedPath).TrimStart('.')} file";
                                        
                _processedFiles[normalizedPath] = metadataContent;
                
                Console.WriteLine($"Processed binary: {relativePath} ({FormatFileSize(fileInfo.Length)})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error processing binary file {filePath}: {ex.Message}");
            }
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        private async Task WriteOutputAsync(string outputPath)
        {
            Console.WriteLine($"Writing {_processedFiles.Count} files to output...");

            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

            // Markdown metadata
            await writer.WriteLineAsync("# Solution Bundle");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync($"- **Generated on:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            await writer.WriteLineAsync($"- **Solution:** `{Path.GetFileName(_solutionPath)}`");
            await writer.WriteLineAsync($"- **Total Files:** {_processedFiles.Count}");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("---");
            await writer.WriteLineAsync();

            // Files section
            await writer.WriteLineAsync("## Files");
            await writer.WriteLineAsync();

            foreach (var file in _processedFiles)
            {
                string relativePath;
                //Process the first element differently
                if (file.Key == _processedFiles.First().Key)
                {
                    relativePath = Path.GetRelativePath(_solutionDirectory, file.Key);
                    await writer.WriteLineAsync($"- [`{relativePath}`](#{ToMarkdownAnchor(relativePath)})");
                    continue;
                }
                //Process the spacers
                if(file.Key.Contains(":SPACER-"))
                {
                    await writer.WriteLineAsync($"");
                    await writer.WriteLineAsync($"- {file.Key.Replace(":SPACER-", "").ToUpper()}");
                    continue;
                }
                relativePath = Path.GetRelativePath(_solutionDirectory, file.Key);
                await writer.WriteLineAsync($"\t- [`{relativePath}`](#{ToMarkdownAnchor(relativePath)})");
            }

            await writer.WriteLineAsync();
            await writer.WriteLineAsync("---");
            await writer.WriteLineAsync();

            // Write file contents
            foreach (var file in _processedFiles)
            {
                string relativePath = Path.GetRelativePath(_solutionDirectory, file.Key);
                string extension = Path.GetExtension(file.Key);

                await writer.WriteLineAsync($"### {relativePath}");
                await writer.WriteLineAsync();

                string language = extension.TrimStart('.').ToLower() switch
                {
                    "cs" => "csharp",
                    "vb" => "vb",
                    "fs" => "fsharp",
                    "js" => "javascript",
                    "ts" => "typescript",
                    "json" => "json",
                    "xml" => "xml",
                    "md" => "markdown",
                    "html" or "cshtml" or "aspx" or "razor" => "html",
                    "css" => "css",
                    "sql" => "sql",
                    "yml" or "yaml" => "yaml",
                    _ => ""
                };

                await writer.WriteLineAsync(!string.IsNullOrEmpty(language) ? $"```{language}" : "```");
                await writer.WriteLineAsync(file.Value);
                await writer.WriteLineAsync("```");
                await writer.WriteLineAsync();
            }
        }

        // Helper to generate a Markdown-friendly anchor name
        private string ToMarkdownAnchor(string text)
        {
            var anchor = text.ToLower()
                             .Replace(" ", "-")
                             .Replace(".", "")
                             .Replace("/", "")
                             .Replace("\\", "")
                             .Replace("`", "")
                             .Replace("#", "");
            return anchor;
        }


    }
}