using System.Globalization;
using Geeratya.AnsiToSvg;

const string usageText = "usage: ansitosvg --cols <n> --rows <n> [--theme dark|light] < capture.ansi > out.svg";

var arguments = new Dictionary<string, string>();

for (var index = 0; index + 1 < args.Length; index += 2)
{
    arguments[args[index]] = args[index + 1];
}

var theme = SvgTheme.ByName(arguments.GetValueOrDefault("--theme", "dark"));
var columns = ParsePositive(arguments.GetValueOrDefault("--cols"));
var rows = ParsePositive(arguments.GetValueOrDefault("--rows"));

if (columns == 0 || rows == 0 || theme is null || args.Length % 2 != 0)
{
    Console.Error.WriteLine(usageText);

    return 2;
}

var input = Console.In.ReadToEnd();
Console.Out.Write(SvgWriter.Write(AnsiScreen.Parse(input, columns, rows), theme));

return 0;

static int ParsePositive(string? text)
{
    return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
}
