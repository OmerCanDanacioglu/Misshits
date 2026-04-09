namespace Misshits.Desktop.Models;

public record KeyDef(
    string Label,
    string Code,
    double Width = 1.0,
    bool Special = false,
    string? SubLabel = null,
    string? ShiftLabel = null
);
