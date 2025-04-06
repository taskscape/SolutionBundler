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
            List<string> projectFiles = ParseSolutionForProjects(_solutionPath);
            Console.WriteLine($"Found {projectFiles.Count} project(s) in solution");

            // Process each project file
            foreach (string projectFile in projectFiles)
            {
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
            List<string> projectPaths = new();
            string[] solutionLines = File.ReadAllLines(solutionPath);

            foreach (string line in solutionLines)
            {
                // Project entry format in .sln file:
                // Project("{GUID}") = "ProjectName", "RelativePath\To\Project.csproj", "{ProjectGUID}"
                if (line.StartsWith("Project("))
                {
                    string[] parts = line.Split([','], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string projectPathPart = parts[1].Trim();
                        
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
            
            // Process the project file itself
            await ProcessFileAsync(projectPath);
            
            string projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
            
            // Parse project file to find referenced files
            List<string> includedFiles = ParseProjectForFiles(projectPath);
            
            // Process each directly referenced file
            foreach (string file in includedFiles)
            {
                string fullPath = Path.GetFullPath(Path.Combine(projectDir, file));
                if (File.Exists(fullPath))
                {
                    await ProcessFileAsync(fullPath);
                }
            }
            
            // Recursively process all files in the project directory
            await ProcessDirectoryAsync(projectDir);
        }

        private List<string> ParseProjectForFiles(string projectPath)
        {
            List<string> includedFiles = new List<string>();
            
            try
            {
                XDocument projectXml = XDocument.Load(projectPath);
                XNamespace? xmlns = projectXml.Root?.GetDefaultNamespace();

                // Look for Include attributes on common item nodes
                List<XElement>? itemGroups = projectXml.Root?.Elements()
                    .Where(e => e.Name.LocalName == "ItemGroup")
                    .ToList();

                if (itemGroups != null)
                {
                    foreach (XElement itemGroup in itemGroups)
                    {
                        List<string> items = itemGroup.Elements()
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
                    string ext = Path.GetExtension(file);
                    if (_textFileExtensions.Contains(ext))
                    {
                        await ProcessFileAsync(file);
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
            // Skip if we already processed this file
            string normalizedPath = Path.GetFullPath(filePath);
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

        private async Task WriteOutputAsync(string outputPath)
        {
            Console.WriteLine($"Writing {_processedFiles.Count} files to output...");

            await using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
            
            // Write a JSON header with metadata
            var metadata = new
            {
                solution = Path.GetFileName(_solutionPath),
                timestamp = DateTime.UtcNow.ToString("o"),
                fileCount = _processedFiles.Count
            };
            
            await writer.WriteLineAsync("# SOLUTION BUNDLE");
            await writer.WriteLineAsync($"# Generated on {DateTime.Now}");
            await writer.WriteLineAsync($"# Solution: {Path.GetFileName(_solutionPath)}");
            await writer.WriteLineAsync($"# Total Files: {_processedFiles.Count}");
            await writer.WriteLineAsync("# ---------------------");
            await writer.WriteLineAsync();
            
            // Write each file with content markers
            foreach (KeyValuePair<string, string> file in _processedFiles.OrderBy(f => f.Key))
            {
                string relativePath = Path.GetRelativePath(_solutionDirectory, file.Key);
                string extension = Path.GetExtension(file.Key);
                
                await writer.WriteLineAsync($"## FILE: {relativePath}");
                
                // Add language markers for syntax highlighting if an LLM supports it
                if (!string.IsNullOrEmpty(extension))
                {
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
                    
                    if (!string.IsNullOrEmpty(language))
                    {
                        await writer.WriteLineAsync($"```{language}");
                    }
                    else
                    {
                        await writer.WriteLineAsync("```");
                    }
                }
                else
                {
                    await writer.WriteLineAsync("```");
                }
                
                await writer.WriteLineAsync(file.Value);
                await writer.WriteLineAsync("```");
                await writer.WriteLineAsync();
            }
        }
    }
}