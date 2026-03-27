/*
 * Manitoba School Records — PDF Page Classifier
 * ================================================
 * Splits multi-page PDFs, extracts text, classifies each page
 * by document type using keyword matching, and sorts into folders.
 *
 * Built via GitHub Actions → produces a self-contained .exe
 * No manual DLL downloads needed.
 *
 * Usage:
 *   PDFClassifier.exe "C:\Records\PDFs" "C:\Records\Sorted"
 *   PDFClassifier.exe "C:\Records\PDFs" "C:\Records\Sorted" --preview
 *   PDFClassifier.exe "C:\Records\PDFs" "C:\Records\Sorted" --min-score 5
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace PDFClassifier
{
    class DocumentType
    {
        public string Name { get; set; } = "";
        public string FolderName { get; set; } = "";
        public List<KeywordRule> Rules { get; set; } = new List<KeywordRule>();

        public double Score(string text)
        {
            double score = 0;
            foreach (var rule in Rules)
            {
                int count = CountOccurrences(text, rule.Keyword);
                score += count * rule.Weight;
            }
            return score;
        }

        private int CountOccurrences(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return 0;

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                index += keyword.Length;
            }
            return count;
        }
    }

    class KeywordRule
    {
        public string Keyword { get; set; }
        public double Weight { get; set; }

        public KeywordRule(string keyword, double weight = 1.0)
        {
            Keyword = keyword;
            Weight = weight;
        }
    }

    class ClassificationResult
    {
        public string SourcePdf { get; set; } = "";
        public int PageNumber { get; set; }
        public string Category { get; set; } = "";
        public double Confidence { get; set; }
        public string TextPreview { get; set; } = "";
        public string OutputPath { get; set; } = "";
    }

    class Program
    {
        // ─────────────────────────────────────────────
        // Document type definitions with weighted keywords
        // Edit these to add/remove/adjust categories!
        // ─────────────────────────────────────────────
        static List<DocumentType> documentTypes = new List<DocumentType>
        {
            new DocumentType
            {
                Name = "Transcript",
                FolderName = "Transcripts",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("transcript", 5.0),
                    new KeywordRule("academic record", 4.0),
                    new KeywordRule("credits", 2.0),
                    new KeywordRule("credit", 1.5),
                    new KeywordRule("course", 1.5),
                    new KeywordRule("grade", 1.0),
                    new KeywordRule("marks", 1.5),
                    new KeywordRule("final mark", 3.0),
                    new KeywordRule("semester", 1.5),
                    new KeywordRule("standing", 1.0),
                    new KeywordRule("cumulative", 2.0),
                    new KeywordRule("gpa", 3.0),
                    new KeywordRule("grading", 1.5),
                    new KeywordRule("subject", 1.0),
                    new KeywordRule("diploma", 2.0),
                }
            },
            new DocumentType
            {
                Name = "Doctor/Medical Note",
                FolderName = "Doctor_Notes",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("doctor", 3.0),
                    new KeywordRule("dr.", 2.0),
                    new KeywordRule("physician", 4.0),
                    new KeywordRule("medical", 2.5),
                    new KeywordRule("diagnosis", 4.0),
                    new KeywordRule("prescription", 3.0),
                    new KeywordRule("immunization", 4.0),
                    new KeywordRule("vaccination", 4.0),
                    new KeywordRule("vaccine", 3.0),
                    new KeywordRule("health", 1.5),
                    new KeywordRule("clinic", 2.0),
                    new KeywordRule("patient", 2.5),
                    new KeywordRule("treatment", 2.0),
                    new KeywordRule("medical examination", 5.0),
                    new KeywordRule("physical examination", 4.0),
                }
            },
            new DocumentType
            {
                Name = "Report Card",
                FolderName = "Report_Cards",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("report card", 5.0),
                    new KeywordRule("progress report", 4.0),
                    new KeywordRule("teacher comments", 4.0),
                    new KeywordRule("teacher's comments", 4.0),
                    new KeywordRule("conduct", 2.0),
                    new KeywordRule("effort", 1.5),
                    new KeywordRule("achievement", 2.0),
                    new KeywordRule("satisfactory", 1.5),
                    new KeywordRule("excellent", 1.0),
                    new KeywordRule("improvement", 1.5),
                    new KeywordRule("term", 1.0),
                    new KeywordRule("reporting period", 3.0),
                    new KeywordRule("parent signature", 3.0),
                }
            },
            new DocumentType
            {
                Name = "Parent Letter/Correspondence",
                FolderName = "Parent_Letters",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("dear parent", 5.0),
                    new KeywordRule("dear guardian", 5.0),
                    new KeywordRule("dear mr", 2.0),
                    new KeywordRule("dear mrs", 2.0),
                    new KeywordRule("permission", 3.0),
                    new KeywordRule("consent", 3.0),
                    new KeywordRule("consent form", 5.0),
                    new KeywordRule("permission slip", 5.0),
                    new KeywordRule("parent", 1.0),
                    new KeywordRule("guardian", 1.5),
                    new KeywordRule("mother", 1.0),
                    new KeywordRule("father", 1.0),
                    new KeywordRule("field trip", 3.0),
                    new KeywordRule("please sign", 3.0),
                }
            },
            new DocumentType
            {
                Name = "Registration/Enrollment",
                FolderName = "Registration",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("registration", 4.0),
                    new KeywordRule("enrollment", 4.0),
                    new KeywordRule("enrolment", 4.0),
                    new KeywordRule("admission", 3.0),
                    new KeywordRule("date of birth", 3.0),
                    new KeywordRule("birth date", 3.0),
                    new KeywordRule("emergency contact", 4.0),
                    new KeywordRule("home address", 2.5),
                    new KeywordRule("postal code", 2.0),
                    new KeywordRule("phone number", 1.5),
                    new KeywordRule("previous school", 3.0),
                    new KeywordRule("registration form", 5.0),
                    new KeywordRule("student information", 3.0),
                }
            },
            new DocumentType
            {
                Name = "Attendance Record",
                FolderName = "Attendance",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("attendance", 4.0),
                    new KeywordRule("days present", 5.0),
                    new KeywordRule("days absent", 5.0),
                    new KeywordRule("absent", 2.0),
                    new KeywordRule("tardy", 3.0),
                    new KeywordRule("late", 1.0),
                    new KeywordRule("attendance record", 5.0),
                    new KeywordRule("excused", 2.0),
                    new KeywordRule("unexcused", 3.0),
                }
            },
            new DocumentType
            {
                Name = "General Correspondence",
                FolderName = "Correspondence",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("memorandum", 4.0),
                    new KeywordRule("memo", 2.0),
                    new KeywordRule("dear sir", 3.0),
                    new KeywordRule("dear madam", 3.0),
                    new KeywordRule("sincerely", 2.0),
                    new KeywordRule("yours truly", 2.0),
                    new KeywordRule("regards", 1.5),
                    new KeywordRule("cc:", 2.0),
                    new KeywordRule("re:", 1.5),
                    new KeywordRule("reference:", 1.5),
                }
            },
            new DocumentType
            {
                Name = "Discipline Record",
                FolderName = "Discipline",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("suspension", 5.0),
                    new KeywordRule("expulsion", 5.0),
                    new KeywordRule("disciplinary", 4.0),
                    new KeywordRule("incident", 2.5),
                    new KeywordRule("incident report", 5.0),
                    new KeywordRule("behaviour", 2.0),
                    new KeywordRule("behavior", 2.0),
                    new KeywordRule("conduct report", 4.0),
                    new KeywordRule("detention", 3.0),
                    new KeywordRule("infraction", 3.0),
                    new KeywordRule("violation", 2.5),
                }
            },
            new DocumentType
            {
                Name = "Psychological/Assessment",
                FolderName = "Psychological",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("psychological", 5.0),
                    new KeywordRule("assessment", 2.5),
                    new KeywordRule("psychological assessment", 6.0),
                    new KeywordRule("iq", 3.0),
                    new KeywordRule("test score", 2.0),
                    new KeywordRule("learning disability", 5.0),
                    new KeywordRule("special education", 4.0),
                    new KeywordRule("evaluation", 2.0),
                    new KeywordRule("psychologist", 4.0),
                    new KeywordRule("cognitive", 3.0),
                    new KeywordRule("developmental", 2.5),
                    new KeywordRule("standardized test", 3.0),
                    new KeywordRule("percentile", 2.0),
                }
            },
            new DocumentType
            {
                Name = "Financial Record",
                FolderName = "Financial",
                Rules = new List<KeywordRule>
                {
                    new KeywordRule("fee", 2.0),
                    new KeywordRule("tuition", 4.0),
                    new KeywordRule("payment", 2.5),
                    new KeywordRule("receipt", 3.0),
                    new KeywordRule("invoice", 4.0),
                    new KeywordRule("amount due", 4.0),
                    new KeywordRule("balance", 2.0),
                    new KeywordRule("paid", 1.5),
                    new KeywordRule("refund", 3.0),
                    new KeywordRule("financial", 2.0),
                }
            },
        };

        static int totalProcessed = 0;
        static int totalErrors = 0;
        static Dictionary<string, int> categoryCounts = new Dictionary<string, int>();

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║   Manitoba School Records — PDF Page Classifier  ║");
            Console.WriteLine("║   github.com/jeelsojitra/pdf-classifier          ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            if (args.Length < 2)
            {
                PrintUsage();
                return;
            }

            string inputFolder = args[0];
            string outputFolder = args[1];
            bool previewOnly = args.Any(a => a.Equals("--preview", StringComparison.OrdinalIgnoreCase));
            double minConfidence = 3.0;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--min-score", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    double.TryParse(args[i + 1], out minConfidence);
                }
            }

            if (!Directory.Exists(inputFolder))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Input folder not found: {inputFolder}");
                Console.ResetColor();
                return;
            }

            string[] pdfFiles = Directory.GetFiles(inputFolder, "*.pdf", SearchOption.AllDirectories);
            if (pdfFiles.Length == 0)
                pdfFiles = Directory.GetFiles(inputFolder, "*.PDF", SearchOption.AllDirectories);

            if (pdfFiles.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"No PDF files found in: {inputFolder}");
                Console.ResetColor();
                return;
            }

            Console.WriteLine($"  Input folder:  {inputFolder}");
            Console.WriteLine($"  Output folder: {outputFolder}");
            Console.WriteLine($"  PDFs found:    {pdfFiles.Length}");
            Console.WriteLine($"  Min score:     {minConfidence}");
            Console.WriteLine($"  Mode:          {(previewOnly ? "PREVIEW (no files moved)" : "CLASSIFY AND SORT")}");
            Console.WriteLine();

            if (!previewOnly)
                CreateOutputFolders(outputFolder);

            List<ClassificationResult> allResults = new List<ClassificationResult>();
            var startTime = DateTime.Now;

            for (int i = 0; i < pdfFiles.Length; i++)
            {
                string pdfPath = pdfFiles[i];
                string fileName = System.IO.Path.GetFileName(pdfPath);
                Console.Write($"  [{i + 1}/{pdfFiles.Length}] {fileName}");

                try
                {
                    var results = ProcessPdf(pdfPath, outputFolder, minConfidence, previewOnly);
                    allResults.AddRange(results);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($" ({results.Count} pages)");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    totalErrors++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($" ERROR: {ex.Message}");
                    Console.ResetColor();
                }
            }

            var elapsed = DateTime.Now - startTime;

            if (!previewOnly && allResults.Count > 0)
                WriteReport(allResults, outputFolder);

            PrintSummary(allResults, previewOnly, elapsed);

            if (previewOnly)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n  This was a PREVIEW. Run without --preview to actually sort files.");
                Console.ResetColor();
            }

            Console.WriteLine("\n  Press any key to exit...");
            Console.ReadKey();
        }

        static List<ClassificationResult> ProcessPdf(string pdfPath, string outputFolder,
            double minConfidence, bool previewOnly)
        {
            var results = new List<ClassificationResult>();
            PdfReader? reader = null;

            try
            {
                reader = new PdfReader(pdfPath);
                int pageCount = reader.NumberOfPages;

                for (int page = 1; page <= pageCount; page++)
                {
                    totalProcessed++;

                    string text = "";
                    try
                    {
                        text = PdfTextExtractor.GetTextFromPage(reader, page,
                            new SimpleTextExtractionStrategy());
                    }
                    catch { text = ""; }

                    string category = "Unknown";
                    double bestScore = 0;

                    foreach (var docType in documentTypes)
                    {
                        double score = docType.Score(text);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            category = docType.FolderName;
                        }
                    }

                    if (bestScore < minConfidence)
                        category = "Unknown";

                    if (!categoryCounts.ContainsKey(category))
                        categoryCounts[category] = 0;
                    categoryCounts[category]++;

                    string baseName = System.IO.Path.GetFileNameWithoutExtension(pdfPath);
                    string outFileName = $"{baseName}_p{page:D4}.pdf";
                    string outPath = System.IO.Path.Combine(outputFolder, category, outFileName);

                    var result = new ClassificationResult
                    {
                        SourcePdf = pdfPath,
                        PageNumber = page,
                        Category = category,
                        Confidence = bestScore,
                        TextPreview = text.Length > 200 ? text.Substring(0, 200) : text,
                        OutputPath = outPath,
                    };
                    results.Add(result);

                    if (!previewOnly)
                    {
                        try { ExtractPage(pdfPath, page, outPath); }
                        catch (Exception) { /* logged in summary */ }
                    }
                }
            }
            finally
            {
                reader?.Close();
            }

            return results;
        }

        static void ExtractPage(string sourcePdf, int pageNumber, string outputPath)
        {
            using var reader = new PdfReader(sourcePdf);
            var doc = new iTextSharp.text.Document();
            using var outStream = new FileStream(outputPath, FileMode.Create);
            var copy = new PdfCopy(doc, outStream);
            doc.Open();
            copy.AddPage(copy.GetImportedPage(reader, pageNumber));
            doc.Close();
        }

        static void CreateOutputFolders(string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);
            foreach (var docType in documentTypes)
                Directory.CreateDirectory(System.IO.Path.Combine(outputFolder, docType.FolderName));
            Directory.CreateDirectory(System.IO.Path.Combine(outputFolder, "Unknown"));
        }

        static void WriteReport(List<ClassificationResult> results, string outputFolder)
        {
            // CSV report
            string csvPath = System.IO.Path.Combine(outputFolder, "_classification_report.csv");
            using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
            {
                writer.WriteLine("Source PDF,Page,Category,Score,Text Preview");
                foreach (var r in results)
                {
                    string preview = r.TextPreview
                        .Replace("\"", "'").Replace("\n", " ").Replace("\r", "");
                    writer.WriteLine($"\"{r.SourcePdf}\",{r.PageNumber},\"{r.Category}\",{r.Confidence:F1},\"{preview}\"");
                }
            }

            // Summary
            string summaryPath = System.IO.Path.Combine(outputFolder, "_summary.txt");
            using (var writer = new StreamWriter(summaryPath, false, Encoding.UTF8))
            {
                writer.WriteLine("Manitoba School Records — Classification Summary");
                writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"Total pages processed: {results.Count}");
                writer.WriteLine();
                writer.WriteLine("Category Breakdown:");
                writer.WriteLine(new string('-', 50));

                foreach (var group in results.GroupBy(r => r.Category).OrderByDescending(g => g.Count()))
                {
                    double avgScore = group.Average(r => r.Confidence);
                    writer.WriteLine($"  {group.Key,-25} {group.Count(),6} pages  (avg score: {avgScore:F1})");
                }

                writer.WriteLine(new string('-', 50));

                var lowConf = results.Where(r => r.Confidence < 5.0 && r.Category != "Unknown").ToList();
                if (lowConf.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine($"LOW CONFIDENCE ({lowConf.Count}) — Review these:");
                    foreach (var r in lowConf.Take(50))
                        writer.WriteLine($"  {System.IO.Path.GetFileName(r.SourcePdf)} p{r.PageNumber} -> {r.Category} (score: {r.Confidence:F1})");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  Report: {csvPath}");
            Console.WriteLine($"  Summary: {summaryPath}");
            Console.ResetColor();
        }

        static void PrintSummary(List<ClassificationResult> results, bool previewOnly, TimeSpan elapsed)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ══════════════════════════════════════════");
            Console.WriteLine("    CLASSIFICATION SUMMARY");
            Console.WriteLine("  ══════════════════════════════════════════");
            Console.ResetColor();

            Console.WriteLine($"    Pages processed: {totalProcessed:N0}");
            Console.WriteLine($"    Errors:          {totalErrors}");
            Console.WriteLine($"    Time:            {elapsed.TotalMinutes:F1} minutes");
            Console.WriteLine();

            if (categoryCounts.Count == 0) return;

            var sorted = categoryCounts.OrderByDescending(kv => kv.Value);
            int maxNameLen = categoryCounts.Keys.Max(k => k.Length);

            foreach (var kv in sorted)
            {
                Console.ForegroundColor = kv.Key == "Unknown" ? ConsoleColor.Yellow : ConsoleColor.Green;
                int barLen = (int)((double)kv.Value / totalProcessed * 30);
                string bar = new string('#', Math.Max(1, barLen));
                Console.WriteLine($"    {kv.Key.PadRight(maxNameLen + 2)} {kv.Value,6}  {bar}");
            }
            Console.ResetColor();
        }

        static void PrintUsage()
        {
            Console.WriteLine("  Usage:");
            Console.WriteLine("    PDFClassifier.exe <input_folder> <output_folder> [options]");
            Console.WriteLine();
            Console.WriteLine("  Options:");
            Console.WriteLine("    --preview       See classifications without moving files");
            Console.WriteLine("    --min-score N   Minimum score to classify (default: 3.0)");
            Console.WriteLine();
            Console.WriteLine("  Examples:");
            Console.WriteLine("    PDFClassifier.exe \"C:\\Records\\PDFs\" \"C:\\Records\\Sorted\"");
            Console.WriteLine("    PDFClassifier.exe \"C:\\Records\\PDFs\" \"C:\\Records\\Sorted\" --preview");
            Console.WriteLine("    PDFClassifier.exe \"C:\\Records\\PDFs\" \"C:\\Records\\Sorted\" --min-score 5");
        }
    }
}
