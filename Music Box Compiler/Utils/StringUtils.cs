namespace MusicBoxCompiler.Utils;

public static class StringUtils
{
    public static string[] SplitString(string value, char separator)
    {
        return string.IsNullOrWhiteSpace(value) ? [] : value.Split(separator);
    }
}
