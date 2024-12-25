using BlueprintGenerator.Constants;

namespace CompilerCommon;

public static class SignalUtils
{
    /// <summary>
    /// The maximum number of signals the compiler supports in a single memory cell.
    /// </summary>
    public const int MaxSignals = 200;

    public static string GetSignalByNumber(int signalNumber)
    {
        return ComputerSignals.OrderedSignals[signalNumber - 1];
    }
}
