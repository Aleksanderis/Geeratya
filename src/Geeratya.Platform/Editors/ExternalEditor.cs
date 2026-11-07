using System.Text;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Editors;

/// <summary>
/// Edits text in <c>$VISUAL</c>, else <c>$EDITOR</c> (Windows: else <c>notepad</c>; Linux and macOS: else a failed
/// result that names the variables, because no editor is a safe guess). The text goes to a new file in the temporary
/// folder (mode 0600 from creation on Linux and macOS; on Windows the user's temporary folder is already limited to
/// the user, an explicit branch), is read back after the editor exits with code 0 and the file is removed in every case.
/// </summary>
public sealed class ExternalEditor : IExternalEditor
{
    private const string FilePrefix = "geeratya-";
    private const string FileExtension = ".md";
    private const string WindowsFallback = "notepad";
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly UTF8Encoding _utf8 = new(false);

    private readonly OperatingSystemKind _operatingSystem;
    private readonly EnvironmentReader _environment;
    private readonly ITerminalProgramRunner _runner;
    private readonly string _folder;

    public ExternalEditor(
        OperatingSystemKind operatingSystem,
        EnvironmentReader environment,
        ITerminalProgramRunner runner,
        string? folder = null)
    {
        _operatingSystem = operatingSystem;
        _environment = environment;
        _runner = runner;
        _folder = folder ?? Path.GetTempPath();
    }

    public async Task<ExternalEditResult> EditAsync(string text, CancellationToken cancellationToken)
    {
        var command = EditorCommandLine.Split(Configured() ?? string.Empty);

        if (command.Count == 0)
        {
            return ExternalEditResult.Failed("set $VISUAL or $EDITOR to your editor (for example EDITOR=vim)");
        }

        var path = Path.Combine(_folder, $"{FilePrefix}{Guid.NewGuid():N}{FileExtension}");

        try
        {
            await WriteAsync(path, text, cancellationToken);

            var exit = await _runner.RunAsync(command[0], [.. command.Skip(1), path], cancellationToken);

            return exit == 0 ? await ReadAsync(path, text, cancellationToken) : Exited(command[0], exit);
        }
        catch (ProgramNotFoundException)
        {
            return ExternalEditResult.Failed($"'{command[0]}' was not found; check $VISUAL or $EDITOR");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ExternalEditResult.Failed($"the temporary file could not be used ({exception.GetType().Name})");
        }
        finally
        {
            Remove(path);
        }
    }

    private string? Configured()
    {
        var configured = _environment.Get("VISUAL") ?? _environment.Get("EDITOR");

        if (configured is not null || _operatingSystem != OperatingSystemKind.Windows)
        {
            return configured;
        }

        return WindowsFallback;
    }

    private async Task WriteAsync(string path, string text, CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };

        if (_operatingSystem != OperatingSystemKind.Windows && !OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFileMode;
        }

        await using var stream = new FileStream(path, options);

        await stream.WriteAsync(_utf8.GetBytes(text), cancellationToken);
    }

    private static async Task<ExternalEditResult> ReadAsync(string path, string original, CancellationToken token)
    {
        var edited = await File.ReadAllTextAsync(path, _utf8, token);

        // Editors end a file with a line break the user did not type; that alone is no change.
        var same = string.Equals(edited.TrimEnd('\r', '\n'), original.TrimEnd('\r', '\n'), StringComparison.Ordinal);

        return same ? ExternalEditResult.Unchanged : ExternalEditResult.Edited(edited);
    }

    private static ExternalEditResult Exited(string program, int exit)
    {
        return ExternalEditResult.Failed($"'{program}' exited with code {exit}; the text is unchanged");
    }

    private static void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing more to do; the file is in the temporary folder and private.
        }
    }
}
