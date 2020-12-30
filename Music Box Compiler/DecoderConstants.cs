using BlueprintCommon.Constants;
using System.Collections.Generic;

namespace MusicBoxCompiler
{
    public class DecoderConstants
    {
        public static readonly List<string> NoteGroupReferenceSignals = new List<string>
        {
            ItemNames.Pistol,
            ItemNames.SubmachineGun,
            ItemNames.Shotgun,
            ItemNames.CombatShotgun,
            ItemNames.RocketLauncher,
            ItemNames.Flamethrower,
            ItemNames.LandMine,
            ItemNames.FirearmMagazine,
            ItemNames.PiercingRoundsMagazine,
            ItemNames.UraniumRoundsMagazine,
            ItemNames.ShotgunShell,
            ItemNames.PiercingShotgunShell,
            ItemNames.CannonShell,
            ItemNames.ExplosiveCannonShell,
            ItemNames.UraniumCannonShell,
            ItemNames.ExplosiveUraniumCannonShell,
            ItemNames.ArtilleryShell,
            ItemNames.Rocket,
            ItemNames.ExplosiveRocket,
            ItemNames.AtomicBomb,
            ItemNames.FlamethrowerAmmo,
            ItemNames.Grenade,
            ItemNames.ClusterGrenade,
            ItemNames.PoisonCapsule,
            ItemNames.SlowdownCapsule,
            ItemNames.DefenderCapsule,
            ItemNames.DistractorCapsule,
            ItemNames.DestroyerCapsule,
            ItemNames.LightArmor,
            ItemNames.HeavyArmor,
            ItemNames.ModularArmor,
            ItemNames.PowerArmor,
            ItemNames.PowerArmorMk2,
            ItemNames.PortableSolarPanel,
            ItemNames.PortableFusionReactor,
            ItemNames.PersonalBattery,
            ItemNames.PersonalBatteryMk2,
            ItemNames.BeltImmunityEquipment,
            ItemNames.Exoskeleton
        };

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
                ItemNames.SolidFuel,
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
                ItemNames.PetroleumGasBarrel,
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
                ItemNames.EngineUnit,
            }),
            new(new List<string>
            { // Group 4: 10 signals
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
            }),
            new(new List<string>
            { // Group 5: 8 signals
                ItemNames.UsedUpUraniumFuelCell,
                ItemNames.AutomationSciencePack,
                ItemNames.LogisticSciencePack,
                ItemNames.MilitarySciencePack,
                ItemNames.ChemicalSciencePack,
                ItemNames.ProductionSciencePack,
                ItemNames.UtilitySciencePack,
                ItemNames.SpaceSciencePack,
            }),
            new(new List<string>
            { // Group 6: 6 signals
                ItemNames.WoodenChest,
                ItemNames.IronChest,
                ItemNames.SteelChest,
                ItemNames.StorageTank,
                ItemNames.TransportBelt,
                ItemNames.FastTransportBelt,
            }),
            new(new List<string>
            { // Group 7: 5 signals
                ItemNames.ExpressTransportBelt,
                ItemNames.UndergroundBelt,
                ItemNames.FastUndergroundBelt,
                ItemNames.ExpressUndergroundBelt,
                ItemNames.Splitter,
            }),
            new(new List<string>
            { // Group 8: 5 signals
                ItemNames.FastSplitter,
                ItemNames.ExpressSplitter,
                ItemNames.BurnerInserter,
                ItemNames.Inserter,
                ItemNames.LongHandedInserter,
            }),
            new(new List<string>
            { // Group 9: 4 signals
                ItemNames.FastInserter,
                ItemNames.FilterInserter,
                ItemNames.StackInserter,
                ItemNames.StackFilterInserter,
            }),
            new(new List<string>
            { // Group 10: 4 signals
                ItemNames.SmallElectricPole,
                ItemNames.MediumElectricPole,
                ItemNames.BigElectricPole,
                ItemNames.Substation,
            }),
            new(new List<string>
            { // Group 11: 3 signals
                ItemNames.Pipe,
                ItemNames.PipeToGround,
                ItemNames.Pump,
            }),
            new(new List<string>
            { // Group 12: 3 signals
                ItemNames.Rail,
                ItemNames.TrainStop,
                ItemNames.RailSignal,
            }),
            new(new List<string>
            { // Group 13: 3 signals
                ItemNames.RailChainSignal,
                ItemNames.Locomotive,
                ItemNames.CargoWagon,
            }),
            new(new List<string>
            { // Group 14: 2 signals
                ItemNames.FluidWagon,
                ItemNames.ArtilleryWagon,
            }),
            new(new List<string>
            { // Group 15: 2 signals
                ItemNames.Car,
                ItemNames.Tank,
            }),
            new(new List<string>
            { // Group 16: 2 signals
                ItemNames.Spidertron,
                ItemNames.SpidertronRemote,
            }),
            new(new List<string>
            { // Group 17: 2 signals
                ItemNames.LogisticRobot,
                ItemNames.ConstructionRobot,
            }),
            new(new List<string>
            { // Group 18: 2 signals
                ItemNames.ActiveProviderChest,
                ItemNames.PassiveProviderChest,
            }),
            new(new List<string>
            { // Group 19: 2 signal
                ItemNames.StorageChest,
                ItemNames.BufferChest,
            }),
            new(new List<string>
            { // Group 20: 2 signal
                ItemNames.RequesterChest,
                ItemNames.Roboport,
            }),
            new(new List<string>
            { // Group 21: 1 signal
                ItemNames.Lamp,
            }),
            new(new List<string>
            { // Group 22: 1 signal
                ItemNames.RedWire,
            }),
            new(new List<string>
            { // Group 23: 1 signal
                ItemNames.GreenWire,
            }),
            new(new List<string>
            { // Group 24: 1 signal
                ItemNames.ArithmeticCombinator,
            }),
            new(new List<string>
            { // Group 25: 1 signal
                ItemNames.DeciderCombinator,
            }),
            new(new List<string>
            { // Group 26: 1 signal
                ItemNames.ConstantCombinator,
            }),
            new(new List<string>
            { // Group 27: 1 signal
                ItemNames.PowerSwitch,
            }),
            new(new List<string>
            { // Group 28: 1 signal
                ItemNames.ProgrammableSpeaker,
            }),
            new(new List<string>
            { // Group 29: 1 signal
                ItemNames.StoneBrick,
            }),
            new(new List<string>
            { // Group 30: 1 signal
                ItemNames.Concrete,
            }),
            new(new List<string>
            { // Group 31: 1 signal
                ItemNames.HazardConcrete,
            }),
            new(new List<string>
            { // Group 32: 1 signal
                ItemNames.RefinedConcrete,
            })
        };

        public record NoteGroupSignals(List<string> NoteSignals);
    }
}
