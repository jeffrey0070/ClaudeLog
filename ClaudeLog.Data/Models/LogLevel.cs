namespace ClaudeLog.Data.Models;

/// <summary>
/// Log severity levels for diagnostic and error logging
/// </summary>
public enum LogLevel
{
    /// <summary>Detailed trace information for debugging</summary>
    Trace = 0,

    /// <summary>Diagnostic information</summary>
    Debug = 1,

    /// <summary>Informational messages about normal operation</summary>
    Info = 2,

    /// <summary>Warning messages for potentially problematic situations</summary>
    Warning = 3,

    /// <summary>Error messages for failures that don't stop the application</summary>
    Error = 4,

    /// <summary>Critical failures that may cause application termination</summary>
    Critical = 5
}
