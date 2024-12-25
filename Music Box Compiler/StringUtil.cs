namespace MusicBoxCompiler;

public static class StringUtil
{
    public static string[] SplitString(string value, char separator)
    {
        return string.IsNullOrWhiteSpace(value) ? [] : value.Split(separator);
    }
}
