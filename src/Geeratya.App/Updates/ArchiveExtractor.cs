using System.Formats.Tar;
using System.IO.Compression;
using Geeratya.Platform.Updates;

namespace Geeratya.App.Updates;

/// <summary>
/// Takes the program out of a release archive and writes it to one chosen file. Nothing else in the archive is
/// extracted, and the name inside the archive never decides where the file goes, so a crafted archive cannot write
/// elsewhere.
/// </summary>
public static class ArchiveExtractor
{
    /// <summary>Writes the program of <paramref name="archive"/> to <paramref name="destination"/>.</summary>
    /// <exception cref="UpdateFailedException">The archive is unreadable or holds no such program.</exception>
    public static void ExtractProgram(string archive, ReleaseTarget target, string destination)
    {
        try
        {
            if (!Extract(archive, target, destination))
            {
                throw new UpdateFailedException($"the archive holds no {target.BinaryName}");
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or FormatException)
        {
            throw new UpdateFailedException($"could not unpack the archive: {exception.Message}", exception);
        }
    }

    private static bool Extract(string archive, ReleaseTarget target, string destination)
    {
        if (target.IsZip)
        {
            return ExtractFromZip(archive, target.BinaryName, destination);
        }

        return ExtractFromTar(archive, target.BinaryName, destination);
    }

    private static bool ExtractFromZip(string archive, string binaryName, string destination)
    {
        using var zip = ZipFile.OpenRead(archive);

        var entry = zip.Entries.FirstOrDefault(candidate => candidate.Name == binaryName);

        if (entry is null)
        {
            return false;
        }

        using var source = entry.Open();

        CopyTo(source, destination);

        return true;
    }

    private static bool ExtractFromTar(string archive, string binaryName, string destination)
    {
        using var file = File.OpenRead(archive);
        using var unzipped = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(unzipped);

        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile
                && entry.DataStream is { } source
                && Path.GetFileName(entry.Name) == binaryName)
            {
                CopyTo(source, destination);

                return true;
            }
        }

        return false;
    }

    private static void CopyTo(Stream source, string destination)
    {
        using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        source.CopyTo(target);
    }
}
