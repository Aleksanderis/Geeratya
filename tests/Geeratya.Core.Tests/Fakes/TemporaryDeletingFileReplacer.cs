using Geeratya.Core.Storage;

namespace Geeratya.Core.Tests.Fakes;

/// <summary>Simulates a replace that fails because the temporary file itself is gone.</summary>
public sealed class TemporaryDeletingFileReplacer : IFileReplacer
{
    public int Attempts { get; private set; }

    public void Replace(string temporary, string target)
    {
        Attempts++;
        File.Delete(temporary);

        throw new FileNotFoundException("The temporary file is gone.", temporary);
    }

    public void Pause(TimeSpan duration)
    {
    }
}
