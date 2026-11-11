using System.Text;

namespace Geeratya.Core.Storage;

/// <summary>
/// Writes a state file through a uniquely named temporary file in the same folder and replaces the target, so a
/// crash never leaves half a file and two instances never share a temporary file. On Windows an antivirus scanner,
/// an indexer, a sync tool or a reader in another instance can hold the target for a moment, which fails the replace
/// with an <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>; the replace is retried a few times
/// with a short backoff (20, 40, 80 and 160 ms) before the last error is thrown. Readers use
/// <see cref="ReadAllText"/> / <see cref="OpenRead"/>, which share with writers and with a replace or delete.
/// </summary>
public sealed class AtomicFile
{
    private const string TemporarySuffix = ".tmp";
    private const int Attempts = 5;
    private const int FirstBackoffMilliseconds = 20;

    private static readonly UTF8Encoding _utf8WithoutBom = new(false);

    private readonly IFileReplacer _replacer;

    public AtomicFile(IFileReplacer? replacer = null)
    {
        _replacer = replacer ?? new SystemFileReplacer();
    }

    /// <summary>Writes the text as UTF-8 without a byte order mark.</summary>
    /// <param name="path">The target file; its folder must exist.</param>
    /// <param name="text">The new content.</param>
    /// <param name="prepare">Called with the temporary path once it is written, before the replace (permissions).</param>
    public void WriteAllText(string path, string text, Action<string>? prepare = null)
    {
        WriteAllBytes(path, _utf8WithoutBom.GetBytes(text), prepare);
    }

    public void WriteAllBytes(string path, byte[] content, Action<string>? prepare = null)
    {
        Write(path, stream => stream.Write(content), prepare);
    }

    /// <summary>Writes through <paramref name="write"/>; on any failure the temporary file is removed quietly.</summary>
    public void Write(string path, Action<Stream> write, Action<string>? prepare = null)
    {
        var temporary = TemporaryPathOf(path);

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
            }

            prepare?.Invoke(temporary);
            ReplaceWithRetry(temporary, path);
        }
        catch
        {
            DeleteQuietly(temporary);

            throw;
        }
    }

    public static string ReadAllText(string path)
    {
        using var reader = new StreamReader(OpenRead(path), _utf8WithoutBom);

        return reader.ReadToEnd();
    }

    public static byte[] ReadAllBytes(string path)
    {
        using var stream = OpenRead(path);
        using var buffer = new MemoryStream();

        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    /// <summary>Opens for reading while allowing writers and a replace or delete of the file by others.</summary>
    public static FileStream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    /// <summary>Deletes a file and swallows the errors, so cleanup never hides the original failure.</summary>
    public static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A temporary file held by a scanner is left behind; the next write uses a new name.
        }
    }

    private static string TemporaryPathOf(string path)
    {
        return $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}{TemporarySuffix}";
    }

    private void ReplaceWithRetry(string temporary, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _replacer.Replace(temporary, path);

                return;
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < Attempts)
            {
                _replacer.Pause(TimeSpan.FromMilliseconds(FirstBackoffMilliseconds << (attempt - 1)));
            }
        }
    }

    private static bool IsRetryable(Exception exception)
    {
        return exception is (IOException or UnauthorizedAccessException)
            and not (DirectoryNotFoundException or FileNotFoundException);
    }
}
