using BlueprintGenerator.Constants;

namespace CompilerCommon
{
    public static class SignalUtils
    {
        /// <summary>
        /// The maximum number of signals in a single memory cell.
        /// </summary>
        public const int MaxSignals = 20;

        public static string GetSignalByNumber(int signalNumber)
        {
            return ComputerSignals.OrderedSignals[signalNumber - 1];
        }
    }
}
