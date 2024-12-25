namespace BlueprintCommon.Constants
{
    public static class VirtualSignalNames
    {
        public const string Everything = "signal-everything";
        public const string Anything = "signal-anything";
        public const string Each = "signal-each";

        public const string Red = "signal-red";
        public const string Green = "signal-green";
        public const string Blue = "signal-blue";
        public const string Yellow = "signal-yellow";
        public const string Pink = "signal-pink";
        public const string Cyan = "signal-cyan";
        public const string White = "signal-white";
        public const string Gray = "signal-grey";
        public const string Black = "signal-black";

        public const string Check = "signal-check";
        public const string Deny = "signal-deny";
        public const string Info = "signal-info";
        public const string Dot = "signal-dot";

        public const string Vertical = "shape-vertical";
        public const string Horizontal = "shape-horizontal";
        public const string Diagonal = "shape-diagonal";
        public const string Curve = "shape-curve";
        public const string Cross = "shape-cross";
        public const string DiagonalCross = "shape-diagonal-cross";
        public const string Corner = "shape-corner";
        public const string TCross = "shape-t";
        public const string Circle = "shape-circle";

        public const string UpArrow = "up-arrow";
        public const string UpRightArrow = "up-right-arrow";
        public const string RightArrow = "right-arrow";
        public const string DownRightArrow = "down-right-arrow";
        public const string DownArrow = "down-arrow";
        public const string DownLeftArrow = "down-left-arrow";
        public const string LeftArrow = "left-arrow";
        public const string UpLeftArrow = "up-left-arrow";

        public const string StackSize = "signal-stack-size";
        public const string Heart = "signal-heart";
        public const string Skull = "signal-skull";
        public const string Ghost = "signal-ghost";

        public static string LetterOrDigit(char letterOrDigit) => $"signal-{letterOrDigit}";
    }
}
