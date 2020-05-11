namespace BlueprintCommon.Constants
{
    public static class VirtualSignalNames
    {
        public const string Info = "signal-info";
        public const string Everything = "signal-everything";

        public static string LetterOrDigit(char letterOrDigit) => $"signal-{letterOrDigit}";
    }
}
