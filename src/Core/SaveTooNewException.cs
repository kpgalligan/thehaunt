namespace TheHaunt.Core;

public sealed class SaveTooNewException : Exception
{
    public int FileVersion { get; }
    public int CurrentVersion { get; }

    public SaveTooNewException(int fileVersion, int currentVersion)
        : base($"Save file has version {fileVersion}, but this build only supports up to version {currentVersion}.")
    {
        FileVersion = fileVersion;
        CurrentVersion = currentVersion;
    }
}
