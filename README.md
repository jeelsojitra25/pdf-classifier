# PDF Page Classifier

Automatically classifies pages from Manitoba school record PDFs (transcripts, doctor's notes, report cards, etc.) and sorts them into categorized folders.

Built for the Government of Manitoba — runs as a **self-contained `.exe`** with no dependencies to install.

[![Build PDF Classifier](https://github.com/jeelsojitra/pdf-classifier/actions/workflows/build.yml/badge.svg)](https://github.com/jeelsojitra/pdf-classifier/actions/workflows/build.yml)

## Download

Go to **[Releases](https://github.com/jeelsojitra/pdf-classifier/releases)** and download `PDFClassifier.exe`.

No .NET runtime, no DLLs, no installs — just the `.exe`.

## Usage

```
PDFClassifier.exe "C:\Records\PDFs" "C:\Records\Sorted"
```

### Preview first (recommended)

See what it would do without touching any files:

```
PDFClassifier.exe "C:\Records\PDFs" "C:\Records\Sorted" --preview
```

### Options

| Flag | Description |
|------|-------------|
| `--preview` | Classify without moving files |
| `--min-score N` | Minimum keyword score to classify a page (default: 3.0) |

## What It Does

1. Opens each PDF in the input folder (including subfolders)
2. Extracts text from every page using iTextSharp
3. Scores each page against 10 document type keyword profiles
4. Extracts each page as a separate single-page PDF
5. Saves it into the matching category folder
6. Generates a CSV report and summary

## Output

```
Sorted/
├── Transcripts/
├── Doctor_Notes/
├── Report_Cards/
├── Parent_Letters/
├── Registration/
├── Attendance/
├── Correspondence/
├── Discipline/
├── Psychological/
├── Financial/
├── Unknown/                          ← unclassified pages
├── _classification_report.csv        ← every page with score
└── _summary.txt                      ← category breakdown
```

## Tuning

Too many pages in `Unknown/`? Lower the threshold:
```
PDFClassifier.exe "C:\PDFs" "C:\Sorted" --min-score 2
```

Wrong classifications? Raise it:
```
PDFClassifier.exe "C:\PDFs" "C:\Sorted" --min-score 6
```

Open `_classification_report.csv` in Excel to review every classification.

## Adding Document Types

Edit `Program.cs` → find the `documentTypes` list → add a new entry:

```csharp
new DocumentType
{
    Name = "My New Type",
    FolderName = "My_New_Type",
    Rules = new List<KeywordRule>
    {
        new KeywordRule("keyword1", 5.0),  // strong signal
        new KeywordRule("keyword2", 2.0),  // supporting
    }
},
```

Push to `main` and GitHub Actions will build a new `.exe` automatically.

## Build Locally

```bash
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

The `.exe` will be in `./publish/`.

## Tech Stack

- C# / .NET 8
- [iTextSharp 5.5.13.4](https://www.nuget.org/packages/iTextSharp/5.5.13.4) (LGPL, auto-downloaded via NuGet)
- GitHub Actions for CI/CD

## License

MIT
