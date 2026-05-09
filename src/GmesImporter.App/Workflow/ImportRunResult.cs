using GmesImporter.Core.Models;

namespace GmesImporter.App.Workflow;

public sealed record ImportRunResult(string SourceFile, ImportSummary Summary);
