# CssExtractor.MSBuild

## Overview

CssExtractor.MSBuild is a custom MSBuild task designed to extract CSS classes and styles from various file types, deduplicate them, and write them to a single output file. This output can be utilized by CSS processors such as Tailwind CSS. The task is configurable via regular expressions, allowing users to specify which files to exclude and how to extract CSS classes.

## Features

- Extracts CSS classes from common file types such as HTML, CSHTML, and Razor.
- Supports configurable regex patterns for file exclusion and class extraction.
- Deduplicates CSS classes to ensure a clean output.
- Outputs the extracted CSS classes to a specified file for further processing.

## Installation

To use CssExtractor.MSBuild in your project, include it as a dependency in your project file (`.csproj`):

```xml
<PackageReference Include="CssExtractor.MSBuild" Version="1.0.0" />
```

## Configuration

You can configure the CssExtractor task in your MSBuild project file. Below is an example configuration:

```xml
<Target Name="ExtractCssClasses">
  <CssExtractorTask 
    OutputFile="path/to/output.css"
    CssExtractorExcludeFiles="(node_modules|\.git)"
    CssExtractorPatterns="\.className\s*{[^}]*}"
    />
</Target>
```

### Properties

- **OutputFile**: The path to the output file where the deduplicated CSS classes will be written.
- **CssExtractorExcludeFiles**: A regex pattern to exclude certain files from the extraction process. Multiple patterns can be specified.
- **CssExtractorPatterns**: A regex pattern to define how CSS classes should be extracted from the files. Multiple patterns can be specified.

## Examples

### Integrating with Tailwind via Tailwind.MSBuild

To integrate the output from CssExtractor.MSBuild with Tailwind, you can create a target in your project file that runs the CssExtractor task before the Tailwind build process:

```xml
<Target Name="Build" BeforeTargets="TailwindBuild">
  <CssExtractorTask 
    OutputFile="path/to/tailwind.css"
    CssExtractorExcludeFiles="(node_modules|\.git)"
    CssExtractorPatterns="\.className\s*{[^}]*}"
    />
</Target>
```

### Executing Tailwind via CLI

If you prefer to execute Tailwind via the command line, you can run the following command after the CssExtractor task has been executed:

```bash
npx tailwindcss -i ./src/input.css -o ./dist/output.css --minify
```

Make sure to adjust the input and output paths according to your project structure.

## License

This project is licensed under the MIT License. See the LICENSE file for more details.