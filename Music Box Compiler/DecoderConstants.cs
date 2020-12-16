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
            }),
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
            }),
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
            }),
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
            }),
            new(new List<string>
            { // Group 5: 7 signals
                ItemNames.UraniumFuelCell,
                ItemNames.UsedUpUraniumFuelCell,
                ItemNames.AutomationSciencePack,
                ItemNames.LogisticSciencePack,
                ItemNames.MilitarySciencePack,
                ItemNames.ChemicalSciencePack,
                ItemNames.ProductionSciencePack
            }),
            new(new List<string>
            { // Group 6: 6 signals
                ItemNames.UtilitySciencePack,
                ItemNames.SpaceSciencePack,
                ItemNames.WoodenChest,
                ItemNames.IronChest,
                ItemNames.SteelChest,
                ItemNames.StorageTank
            }),
            new(new List<string>
            { // Group 7: 5 signals
                ItemNames.TransportBelt,
                ItemNames.FastTransportBelt,
                ItemNames.ExpressTransportBelt,
                ItemNames.UndergroundBelt,
                ItemNames.FastUndergroundBelt
            }),
            new(new List<string>
            { // Group 8: 4 signals
                ItemNames.ExpressUndergroundBelt,
                ItemNames.Splitter,
                ItemNames.FastSplitter,
                ItemNames.ExpressSplitter
            }),
            new(new List<string>
            { // Group 9: 4 signals
                ItemNames.BurnerInserter,
                ItemNames.Inserter,
                ItemNames.LongHandedInserter,
                ItemNames.FastInserter
            }),
            new(new List<string>
            { // Group 10: 3 signals
                ItemNames.FilterInserter,
                ItemNames.StackInserter,
                ItemNames.StackFilterInserter
            }),
            new(new List<string>
            { // Group 11: 3 signals
                ItemNames.SmallElectricPole,
                ItemNames.MediumElectricPole,
                ItemNames.BigElectricPole
            }),
            new(new List<string>
            { // Group 12: 3 signals
                ItemNames.Substation,
                ItemNames.Pipe,
                ItemNames.PipeToGround
            }),
            new(new List<string>
            { // Group 13: 2 signals
                ItemNames.Pump,
                ItemNames.Rail
            }),
            new(new List<string>
            { // Group 14: 2 signals
                ItemNames.TrainStop,
                ItemNames.RailSignal
            }),
            new(new List<string>
            { // Group 15: 2 signals
                ItemNames.RailChainSignal,
                ItemNames.Locomotive
            }),
            new(new List<string>
            { // Group 16: 2 signals
                ItemNames.CargoWagon,
                ItemNames.FluidWagon
            }),
            new(new List<string>
            { // Group 17: 2 signals
                ItemNames.ArtilleryWagon,
                ItemNames.Car
            }),
            new(new List<string>
            { // Group 18: 2 signals
                ItemNames.Tank,
                ItemNames.Spidertron
            })
        };

        public record NoteGroupSignals(List<string> NoteSignals);
    }
}
