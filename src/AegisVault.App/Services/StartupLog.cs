namespace AegisVault.App.Services;

/// <summary>
/// Best-effort local startup timing log. It exists so a slow cold start can be
/// measured instead of guessed at (AtomUI theme initialisation dominates it), and
/// so users can attach the file to a bug report. Nothing leaves the machine and
/// every failure is swallowed: the log must never break a launch.
/// </summary>
public static class StartupLog
{
    private const int MaxLines = 60;

    public static void Append(string line)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AegisVault");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "startup.log");
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
            lines.Add($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {line}");

            if (lines.Count > MaxLines)
            {
                lines.RemoveRange(0, lines.Count - MaxLines);
            }

            File.WriteAllLines(path, lines);
        }
        catch (Exception)
        {
            // Diagnostics only.
        }
    }
}
