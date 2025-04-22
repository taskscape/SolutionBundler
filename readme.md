# Solution Processor for LLM Analysis

A .NET console application that processes all files in a C# solution and concatenates them into a single file suitable for LLM analysis or refactoring.

## Features

- Processes all files included in a specified solution file
- Handles all project files (.csproj, .vbproj, etc.)
- Includes all markdown files, source code, and other text files
- Generates a single output file with file boundaries and syntax highlighting markers
- Excludes binary files and build output directories (bin, obj, etc.)
- Preserves relative paths for better context

## Installation

### Prerequisites

- .NET 9.0 SDK or later

### Building the Application

1. Clone or download this repository  
2. Open a terminal in the project directory  
3. Build the application:

```bash
dotnet build -c Release
```

## Usage

```bash
dotnet run --project SolutionBundler.csproj <path-to-solution-file> [output-file]
```

Or use the compiled executable:

```bash
SolutionBundler <path-to-solution-file> [output-file]
```

If no output file is specified, the program will create `output.md` in the current directory.

### Examples

```bash
# Process a solution and save to the default output.txt
SolutionBundler C:\Projects\MySolution\MySolution.sln

# Process a solution and save to a specific file
SolutionBundler C:\Projects\MySolution\MySolution.sln C:\Temp\solution-bundle.md
```

## Output Format

The generated output file follows this format:

````markdown
# SOLUTION BUNDLE
# Generated on [Date and Time]
# Solution: YourSolution.sln
# Total Files: [Number of Files]
# ---------------------

## FILE: Path/To/File1.cs
```csharp
// File content here
```

## FILE: Path/To/File2.md
```markdown
# Markdown content here
```

...and so on
````

This format:

- Includes clear file boundaries
- Adds code fence markers with appropriate language for syntax highlighting
- Preserves relative paths to maintain project structure context
- Is optimized for LLM analysis with clear separation between files

## Customization

You can easily modify the code to:

- Include or exclude additional file types by modifying the `_textFileExtensions` collection
- Change the output format to suit your specific LLM
- Add more excluded directories to the `_excludedDirectories` collection

## Configuration

The file and directory filters used by the `SolutionProcessor` can be customized via an `appsettings.json`.

The configuration is optional. If omitted, the program will use a set of sensible defaults.

### Configuring `appsettings.json`

Create a file named `appsettings.json` with the following structure:

```json
{
  "FileSettings": {
    "ProjectExtensions": [
      ".csproj", ".vbproj", ".fsproj", ".sqlproj", ".dbproj", ".ccproj", ".vcxproj"
    ],
    "TextFileExtensions": [
      ".cs", ".vb", ".fs", ".sql", ".xml", ".json", ".md", ".txt", ".config",
      ".settings", ".cpp", ".h", ".hpp", ".xaml", ".cshtml", ".html", ".css", ".js",
      ".ts", ".razor", ".resx", ".yml", ".yaml", ".gitignore", ".editorconfig",
      ".props", ".targets", ".manifest", ".asax", ".ashx", ".aspx", ".sln",
      ".csproj", ".vbproj", ".fsproj", ".sqlproj", ".dbproj", ".ccproj", ".vcxproj"
    ],
    "BinaryFileExtensions": [
      ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".tiff", ".webp", ".svg",
      ".dll", ".exe", ".pdb", ".zip", ".tar", ".gz", ".7z", ".rar", ".pdf", ".doc",
      ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".mp3", ".mp4", ".wav", ".avi",
      ".mov", ".wmv", ".flv"
    ],
    "ExcludedDirectories": [
      "bin", "obj", "node_modules", ".vs", ".git", "packages", "x64", "x86"
    ]
  }
}
```

### Property Descriptions

- **ProjectExtensions**  
  A list of file extensions representing project files that define which source files belong to which projects. These typically include `.csproj`, `.vbproj`, etc.

- **TextFileExtensions**  
  A list of file extensions that are treated as human-readable, text-based content. Files matching these extensions will be included in the output bundle with proper formatting.

- **BinaryFileExtensions**  
  A list of file extensions that are considered binary or non-readable. Files matching these extensions will be included in the output bundle.

- **ExcludedDirectories**  
  A list of directory names that should be skipped during recursive traversal of the solution folder. Typically includes build and dependency folders.

### Example Scenario

To include `.csv` and `.tsv` files for data analysis, and exclude the `dist` and `logs` folders, you could modify the settings as follows:

```json
{
  "FileSettings": {
    "TextFileExtensions": [
      ".cs", ".json", ".csv", ".tsv", ".txt"
    ],
    "ExcludedDirectories": [
      "bin", "obj", "dist", "logs"
    ]
  }
}
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.