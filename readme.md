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

- .NET 7.0 SDK or later

### Building the Application

1. Clone or download this repository
2. Open a terminal in the project directory
3. Build the application:

```bash
dotnet build -c Release
```

## Usage

```bash
dotnet run --project SolutionProcessor.csproj <path-to-solution-file> [output-file]
```

Or use the compiled executable:

```bash
SolutionProcessor <path-to-solution-file> [output-file]
```

If no output file is specified, the program will create `output.md` in the current directory.

### Examples

```bash
# Process a solution and save to the default output.txt
SolutionProcessor C:\Projects\MySolution\MySolution.sln

# Process a solution and save to a specific file
SolutionProcessor C:\Projects\MySolution\MySolution.sln C:\Temp\solution-bundle.md
```

## Output Format

The generated output file follows this format:


````
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

## License

This project is licensed under the MIT License - see the LICENSE file for details.