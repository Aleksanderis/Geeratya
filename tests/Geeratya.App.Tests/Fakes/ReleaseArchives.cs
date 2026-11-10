using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Geeratya.App.Tests.Fakes;

/// <summary>Builds release archives like the release workflow does: <c>geeratya-&lt;version&gt;-&lt;platform&gt;/&lt;binary&gt;</c> and more.</summary>
public static class ReleaseArchives
{
    public static byte[] TarGz(string folder, string binaryName, string content)
    {
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, true))
        {
            Add(writer, $"{folder}/LICENSE", "license text");
            Add(writer, $"{folder}/{binaryName}", content);
        }

        return output.ToArray();
    }

    public static byte[] Zip(string folder, string binaryName, string content)
    {
        using var output = new MemoryStream();

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Write(zip.CreateEntry($"{folder}/LICENSE"), "license text");
            Write(zip.CreateEntry($"{folder}/{binaryName}"), content);
        }

        return output.ToArray();
    }

    /// <summary>A <c>SHA256SUMS</c> text with one line for the file.</summary>
    public static string Sums(string name, byte[] bytes)
    {
        return $"{Convert.ToHexStringLower(SHA256.HashData(bytes))}  {name}\n";
    }

    private static void Add(TarWriter writer, string name, string content)
    {
        var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
        {
            DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
        };

        writer.WriteEntry(entry);
    }

    private static void Write(ZipArchiveEntry entry, string content)
    {
        using var stream = entry.Open();

        stream.Write(Encoding.UTF8.GetBytes(content));
    }
}
