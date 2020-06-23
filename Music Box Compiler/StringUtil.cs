namespace MusicBoxCompiler
{
    public static class StringUtil
    {
        public static string[] SplitString(string value, char separator)
        {
            return string.IsNullOrWhiteSpace(value) ? new string[] { } : value.Split(separator);
        }
    }
}
