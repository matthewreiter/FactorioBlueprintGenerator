namespace BlueprintCommon.Constants
{
    public static class VirtualSignalNames
    {
        public const string Everything = "signal-everything";
        public const string Check = "signal-check";
        public const string Info = "signal-info";
        public const string Dot = "signal-dot";

        public static string LetterOrDigit(char letterOrDigit) => $"signal-{letterOrDigit}";
    }
}
