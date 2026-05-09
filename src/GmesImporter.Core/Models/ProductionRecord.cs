namespace GmesImporter.Core.Models;

public sealed record ProductionRecord(
    string WorkOrder,
    string Ebr,
    string Pid,
    DateTime? Date);
