using BlueprintCommon.Constants;
using System.Collections.Generic;

namespace MusicBoxCompiler
{
    public class DecoderConstants
    {
        public static readonly List<NoteGroupSignals> AllNoteGroupSignals = new List<NoteGroupSignals>
        {
            new(new List<string>
            { // Group 1: 10 signals
                ItemNames.Wood,
                ItemNames.Coal,
                ItemNames.Stone,
                ItemNames.IronOre,
                ItemNames.CopperOre,
                ItemNames.UraniumOre,
                ItemNames.RawFish,
                ItemNames.IronPlate,
                ItemNames.CopperPlate,
                ItemNames.SolidFuel
            }, ItemNames.Boiler),
            new(new List<string>
            { // Group 2: 10 signals
                ItemNames.SteelPlate,
                ItemNames.PlasticBar,
                ItemNames.Sulfur,
                ItemNames.Battery,
                ItemNames.Explosives,
                ItemNames.CrudeOilBarrel,
                ItemNames.HeavyOilBarrel,
                ItemNames.LightOilBarrel,
                ItemNames.LubricantBarrel,
                ItemNames.PetroleumGasBarrel
            }, ItemNames.SteamEngine),
            new(new List<string>
            { // Group 3: 10 signals
                ItemNames.SulfuricAcidBarrel,
                ItemNames.WaterBarrel,
                ItemNames.CopperCable,
                ItemNames.IronStick,
                ItemNames.IronGearWheel,
                ItemNames.EmptyBarrel,
                ItemNames.ElectronicCircuit,
                ItemNames.AdvancedCircuit,
                ItemNames.ProcessingUnit,
                ItemNames.EngineUnit
            }, ItemNames.SolarPanel),
            new(new List<string>
            { // Group 4: 9 signals
                ItemNames.ElectricEngineUnit,
                ItemNames.FlyingRobotFrame,
                ItemNames.Satellite,
                ItemNames.RocketControlUnit,
                ItemNames.LowDensityStructure,
                ItemNames.RocketFuel,
                ItemNames.NuclearFuel,
                ItemNames.Uranium235,
                ItemNames.Uranium238
            }, ItemNames.Accumulator),
            new(new List<string>
            { // Group 5: 7 signals
                ItemNames.UraniumFuelCell,
                ItemNames.UsedUpUraniumFuelCell,
                ItemNames.AutomationSciencePack,
                ItemNames.LogisticSciencePack,
                ItemNames.MilitarySciencePack,
                ItemNames.ChemicalSciencePack,
                ItemNames.ProductionSciencePack
            }, ItemNames.NuclearReactor),
            new(new List<string>
            { // Group 6: 6 signals
                ItemNames.UtilitySciencePack,
                ItemNames.SpaceSciencePack,
                ItemNames.WoodenChest,
                ItemNames.IronChest,
                ItemNames.SteelChest,
                ItemNames.StorageTank
            }, ItemNames.HeatPipe),
            new(new List<string>
            { // Group 7: 5 signals
                ItemNames.TransportBelt,
                ItemNames.FastTransportBelt,
                ItemNames.ExpressTransportBelt,
                ItemNames.UndergroundBelt,
                ItemNames.FastUndergroundBelt
            }, ItemNames.HeatExchanger),
            new(new List<string>
            { // Group 8: 4 signals
                ItemNames.ExpressUndergroundBelt,
                ItemNames.Splitter,
                ItemNames.FastSplitter,
                ItemNames.ExpressSplitter
            }, ItemNames.SteamTurbine),
            new(new List<string>
            { // Group 9: 4 signals
                ItemNames.BurnerInserter,
                ItemNames.Inserter,
                ItemNames.LongHandedInserter,
                ItemNames.FastInserter
            }, ItemNames.BurnerMiningDrill),
            new(new List<string>
            { // Group 10: 3 signal
                ItemNames.FilterInserter,
                ItemNames.StackInserter,
                ItemNames.StackFilterInserter
            }, ItemNames.ElectricMiningDrill),
            new(new List<string>
            { // Group 11: 3 signal
                ItemNames.SmallElectricPole,
                ItemNames.MediumElectricPole,
                ItemNames.BigElectricPole
            }, ItemNames.OffshorePump),
            new(new List<string>
            { // Group 12: 3 signal
                ItemNames.Substation,
                ItemNames.Pipe,
                ItemNames.PipeToGround
            }, ItemNames.Pumpjack),
            new(new List<string>
            { // Group 13: 2 signal
                ItemNames.Pump,
                ItemNames.Rail
            }, ItemNames.StoneFurnace),
            new(new List<string>
            { // Group 14: 2 signal
                ItemNames.TrainStop,
                ItemNames.RailSignal
            }, ItemNames.SteelFurnace),
            new(new List<string>
            { // Group 15: 2 signal
                ItemNames.RailChainSignal,
                ItemNames.Locomotive
            }, ItemNames.ElectricFurnace),
            new(new List<string>
            { // Group 16: 2 signal
                ItemNames.CargoWagon,
                ItemNames.FluidWagon
            }, ItemNames.AssemblyingMachine1)
        };

        public record NoteGroupSignals(List<string> NoteSignals, string HistogramSignal);
    }
}
