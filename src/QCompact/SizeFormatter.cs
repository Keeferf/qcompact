using System.Globalization;

namespace QCompact;

public static class SizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Format(long bytes)
    {
        double value = bytes;
        var index = 0;
        while (Math.Abs(value) >= 1024 && index < Units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return value.ToString("N2", CultureInfo.InvariantCulture) + " " + Units[index];
    }
}
