using System.Collections.Generic;

namespace BlueprintEditor.Models
{
    public class Blueprint
    {
        /// <summary>
        /// The name of the blueprint set by the user.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// The color of the label of this blueprint. Optional.
        /// </summary>
        public Color Label_color { get; set; }

        /// <summary>
        /// The icons of the blueprint set by the user.
        /// </summary>
        public List<Icon> Icons { get; set; }

        /// <summary>
        /// The actual content of the blueprint.
        /// </summary>
        public List<Entity> Entities { get; set; }

        /// <summary>
        /// The name of the item that was saved ("blueprint" in vanilla).
        /// </summary>
        public string Item { get; set; }

        /// <summary>
        /// The tiles included in the blueprint.
        /// </summary>
        public List<Tile> Tiles { get; set; }

        /// <summary>
        /// The schedules for trains in this blueprint.
        /// </summary>
        public List<Schedule> Schedules { get; set; }

        /// <summary>
        /// The map version of the map the blueprint was created in.
        /// </summary>
        public long Version { get; set; }
    }

    public class Icon
    {
        /// <summary>
        /// The icon that is displayed.
        /// </summary>
        public SignalID Signal { get; set; }

        /// <summary>
        /// Index of the icon, 1-based.
        /// </summary>
        public int Index { get; set; }
    }

    public class SignalID
    {
        /// <summary>
        /// Type of the signal. Either "item", "fluid" or "virtual".
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Name of the signal prototype this signal is set to.
        /// </summary>
        public string Name { get; set; }
    }

    public class Entity
    {
        /// <summary>
        /// Index of the entity, 1-based.
        /// </summary>
        public int Entity_number { get; set; }

        /// <summary>
        /// Prototype name of the entity (e.g. "offshore-pump").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Position of the entity within the blueprint.
        /// </summary>
        public Position Position { get; set; }

        /// <summary>
        /// Direction of the entity.
        /// </summary>
        public uint? Direction { get; set; }

        /// <summary>
        /// Orientation of cargo wagon or locomotive, value 0 to 1.
        /// </summary>
        public float? Orientation { get; set; }

        public ControlBehavior Control_behavior { get; set; }

        /// <summary>
        /// Object containing information about the connections to other entities formed by red or green wires.
        /// Key 1: First connection point. The default for everything that doesn't have multiple connection points.
        /// Key 2: Second connection point. For example, the "output" part of an arithmetic combinator.
        /// </summary>
        public Dictionary<string, ConnectionPoint> Connections { get; set; }

        /// <summary>
        /// Item requests by this entity, this is what defines the item-request-proxy when the blueprint is placed, optional.
        /// </summary>
        public ItemRequest Items { get; set; }

        /// <summary>
        /// Name of the recipe prototype this assembling machine is set to, optional.
        /// </summary>
        public string Recipe { get; set; }

        /// <summary>
        /// Used by Prototype/Container, optional. The index of the first inaccessible item slot due to limiting with the red "bar". 0-based.
        /// </summary>
        public int? Bar { get; set; }

        /// <summary>
        /// Cargo wagon inventory configuration, optional.
        /// </summary>
        public Inventory Inventory { get; set; }

        /// <summary>
        /// Used by Prototype/InfinityContainer, optional.
        /// </summary>
        public InfinitySettings Infinity_settings { get; set; }

        /// <summary>
        /// Type of the underground belt or loader, optional. Either "input" or "output".
        /// </summary>
        public string Type { get; set; }
    }

    public class Inventory
    {

    }

    public class Schedule
    {

    }

    public class ScheduleRecord
    {

    }

    public class WaitCondition
    {

    }

    public class Tile
    {

    }

    public class Position
    {
        /// <summary>
        /// X position within the blueprint, 0 is the center.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y position within the blueprint, 0 is the center.
        /// </summary>
        public double Y { get; set; }
    }

    /// <summary>
    /// The actual point where a wire is connected to. Contains information about where it is connected to.
    /// </summary>
    public class ConnectionPoint
    {
        /// <summary>
        /// An array of #Connection data object containing all the connections from this point created by red wire.
        /// </summary>
        public List<ConnectionData> Red { get; set; }

        /// <summary>
        /// An array of #Connection data object containing all the connections from this point created by green wire.
        /// </summary>
        public List<ConnectionData> Green { get; set; }
    }

    /// <summary>
    /// Information about a single connection between two connection points.
    /// </summary>
    public class ConnectionData
    {
        /// <summary>
        /// ID of the entity this connection is connected with.
        /// </summary>
        public int Entity_id { get; set; }

        /// <summary>
        /// The circuit connector id of the entity this connection is connected to.
        /// </summary>
        public int? Circuit_id { get; set; }
    }

    public class ItemRequest
    {

    }

    public class ItemFilter
    {

    }

    public class InfinitySettings
    {

    }

    public class InfinityFilter
    {

    }

    public class LogisticFilter
    {

    }

    public class SpeakerParameter
    {

    }

    public class SpeakerAlertParameter
    {

    }

    public class Color
    {

    }

    public class ControlBehavior
    {
        public List<Filter> Filters { get; set; }

        public ArithmeticConditions Arithmetic_conditions { get; set; }

        public DeciderConditions Decider_conditions { get; set; }

        public CircuitCondition Circuit_condition { get; set; }

        public CircuitParameters Circuit_parameters { get; set; }

        public bool? Is_on { get; set; }
    }

    public class Filter
    {
        public SignalID Signal { get; set; }

        public long Count { get; set; }

        /// <summary>
        /// Index of the filter, 1-based.
        /// </summary>
        public int Index { get; set; }
    }

    public class ArithmeticConditions
    {
        public SignalID First_signal { get; set; }

        public SignalID Second_signal { get; set; }

        public string Operation { get; set; }

        public SignalID Output_signal { get; set; }
    }

    public class DeciderConditions : CircuitCondition
    {
        public SignalID Output_signal { get; set; }

        public bool Copy_count_from_input { get; set; }
    }

    public class CircuitCondition
    {
        public SignalID First_signal { get; set; }

        public SignalID Second_signal { get; set; }

        public long Constant { get; set; }

        public string Comparator { get; set; }
    }

    public class CircuitParameters
    {
        public bool Signal_value_is_pitch { get; set; }

        public int Instrument_id { get; set; }

        public int Note_id { get; set; }
    }
}
