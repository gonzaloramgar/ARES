namespace AresAssistant.Helpers;

/// <summary>
/// Utilidades de formateo compartidas entre ventanas de descarga y progreso.
/// Centraliza la conversión de bytes a texto legible para evitar duplicación de código.
/// </summary>
public static class FormatHelper
{
    /// <summary>
    /// Convierte una cantidad de bytes en una cadena legible (B, KB, MB, GB).
    /// </summary>
    /// <param name="bytes">Cantidad de bytes a formatear.</param>
    /// <returns>Cadena formateada con la unidad más apropiada.</returns>
    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824)
            return $"{bytes / 1_073_741_824.0:F2} GB";
        if (bytes >= 1_048_576)
            return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F0} KB";
        return $"{bytes} B";
    }
}
