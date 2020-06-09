using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using System.Collections.Generic;
using System.Linq;

namespace CompilerCommon
{
    public static class SignalUtils
    {
        public const int MaxSignals = 18;

        // The numeric value of the signal is its index plus 1
        private static readonly List<string> OrderedVirtualSignals = Enumerable.Range('0', 10).Concat(Enumerable.Range('A', 26))
            .Select(letterOrDigit => VirtualSignalNames.LetterOrDigit((char)letterOrDigit)).Concat(
            new List<string>
            {
                VirtualSignalNames.Red,
                VirtualSignalNames.Green,
                VirtualSignalNames.Blue,
                VirtualSignalNames.Yellow,
                VirtualSignalNames.Pink,
                VirtualSignalNames.Cyan,
                VirtualSignalNames.White,
                VirtualSignalNames.Gray,
                VirtualSignalNames.Black
            }).ToList();

        // The numeric value of the signal is its index plus 1 plus the number of virtual signals
        private static readonly List<string> OrderedItemNames = new List<string>
        {
            ItemNames.Wood,
            ItemNames.Coal,
            ItemNames.Stone,
            ItemNames.IronOre,
            ItemNames.CopperOre,
            ItemNames.UraniumOre,
            ItemNames.RawFish,
            ItemNames.IronPlate,
            ItemNames.CopperPlate,
            ItemNames.SolidFuel,
            ItemNames.SteelPlate,
            ItemNames.PlasticBar,
            ItemNames.Sulfur,
            ItemNames.Battery,
            ItemNames.Explosives,
            ItemNames.CrudeOilBarrel,
            ItemNames.HeavyOilBarrel,
            ItemNames.LightOilBarrel,
            ItemNames.LubricantBarrel,
            ItemNames.PetroleumGasBarrel,
            ItemNames.SulfuricAcidBarrel,
            ItemNames.WaterBarrel,
            ItemNames.CopperCable,
            ItemNames.IronStick,
            ItemNames.IronGearWheel,
            ItemNames.EmptyBarrel,
            ItemNames.ElectronicCircuit,
            ItemNames.AdvancedCircuit,
            ItemNames.ProcessingUnit,
            ItemNames.EngineUnit,
            ItemNames.ElectricEngineUnit,
            ItemNames.FlyingRobotFrame,
            ItemNames.Satellite,
            ItemNames.RocketControlUnit,
            ItemNames.LowDensityStructure,
            ItemNames.RocketFuel,
            ItemNames.NuclearFuel,
            ItemNames.Uranium235,
            ItemNames.Uranium238,
            ItemNames.UraniumFuelCell,
            ItemNames.UsedUpUraniumFuelCell,
            ItemNames.AutomationSciencePack,
            ItemNames.LogisticSciencePack,
            ItemNames.MilitarySciencePack,
            ItemNames.ChemicalSciencePack
        };

        // The numeric value of the signal is its index plus 1
        private static readonly List<SignalID> OrderedSignals = OrderedVirtualSignals.Select(name => new SignalID { Type = SignalTypes.Virtual, Name = name }).Concat(
            OrderedItemNames.Select(name => new SignalID { Type = SignalTypes.Item, Name = name })).ToList();

        public static SignalID GetSignalByNumber(int signalNumber)
        {
            return OrderedSignals[signalNumber - 1];
        }
    }
}
