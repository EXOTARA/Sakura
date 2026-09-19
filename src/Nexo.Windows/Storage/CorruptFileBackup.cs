namespace Nexo.Windows.Storage;

public static class CorruptFileBackup
{
    public static string? TryPreserve(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var backupPath = filePath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(filePath, backupPath, overwrite: true);
            return backupPath;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
