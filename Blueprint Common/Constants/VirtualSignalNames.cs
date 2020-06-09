namespace BlueprintCommon.Constants
{
    public static class VirtualSignalNames
    {
        public const string Everything = "signal-everything";
        public const string Check = "signal-check";
        public const string Info = "signal-info";
        public const string Dot = "signal-dot";
        public const string Red = "signal-red";
        public const string Green = "signal-green";
        public const string Blue = "signal-blue";
        public const string Yellow = "signal-yellow";
        public const string Pink = "signal-pink";
        public const string Cyan = "signal-cyan";
        public const string White = "signal-white";
        public const string Gray = "signal-grey";
        public const string Black = "signal-black";

        public static string LetterOrDigit(char letterOrDigit) => $"signal-{letterOrDigit}";
    }
}
