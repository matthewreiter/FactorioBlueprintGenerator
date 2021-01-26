using BlueprintCommon.Constants;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlueprintCommon.Models
{
    /// <summary>
    /// Additional documentation: https://wiki.factorio.com/Blueprint_string_format
    /// </summary>
    public class Blueprint
    {
        /// <summary>
        /// The description of the blueprint set by the user.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Building repetition pattern when building by dragging.
        /// </summary>
        [JsonPropertyName("snap-to-grid")]
        public SnapToGrid SnapToGrid { get; set; }

        /// <summary>
        /// Allows the blueprint to snap to the global grid. The reference point shifts the blueprint relative to the grid.
        /// </summary>
        [JsonPropertyName("absolute-snapping")]
        public bool? AbsoluteSnapping { get; set; }

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
        /// The name of the blueprint set by the user.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// The color of the label of this blueprint. Optional.
        /// </summary>
        public Color Label_color { get; set; }

        /// <summary>
        /// The map version of the map the blueprint was created in.
        /// </summary>
        public long Version { get; set; }
    }

    public class SnapToGrid
    {
        /// <summary>
        /// X dimension of building repetition pattern when building by dragging.
        /// </summary>
        public ulong X { get; set; }

        /// <summary>
        /// T dimension of building repetition pattern when building by dragging.
        /// </summary>
        public ulong Y { get; set; }
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

        public static SignalID Create(string name)
        {
            return new SignalID { Type = name.StartsWith("signal-") ? SignalTypes.Virtual : SignalTypes.Item, Name = name };
        }

        public static SignalID CreateVirtual(string name)
        {
            return new SignalID { Type = SignalTypes.Virtual, Name = name };
        }

        public static SignalID CreateLetterOrDigit(char letterOrDigit)
        {
            return CreateVirtual(VirtualSignalNames.LetterOrDigit(letterOrDigit));
        }
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
        public Direction? Direction { get; set; }

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
        /// IDs of the entities to which this entity is connected. Used by power poles.
        /// </summary>
        [JsonPropertyName("neighbours")]
        public List<int> Neighbors { get; set; }

        /// <summary>
        /// Item requests by this entity, this is what defines the item-request-proxy when the blueprint is placed, optional.
        /// 1 or more instances of key/value pairs. Key is the name of the item, string. Value is the amount of items to be requested.
        /// </summary>
        public Dictionary<string, uint> Items { get; set; }

        /// <summary>
        /// Name of the recipe prototype this assembling machine is set to, optional.
        /// </summary>
        public string Recipe { get; set; }

        /// <summary>
        /// Used by Prototype/Container, optional. The index of the first inaccessible item slot due to limiting with the red "bar". 0-based.
        /// </summary>
        public ushort? Bar { get; set; }

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

        /// <summary>
        /// Input priority of the splitter, optional. Either "right" or "left", "none" is omitted.
        /// </summary>
        public string Input_priority { get; set; }

        /// <summary>
        /// Output priority of the splitter, optional. Either "right" or "left", "none" is omitted.
        /// </summary>
        public string Output_priority { get; set; }

        /// <summary>
        /// Filter of the splitter, optional. Name of the item prototype the filter is set to.
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// Filters of the filter inserter or loader, optional.
        /// </summary>
        public List<ItemFilter> Filters { get; set; }

        /// <summary>
        /// Filter mode of the filter inserter, optional. Either "whitelist" or "blacklist".
        /// </summary>
        public string Filter_mode { get; set; }

        /// <summary>
        /// The stack size the inserter is set to, optional.
        /// </summary>
        public byte? Override_stack_size { get; set; }

        /// <summary>
        /// The drop position the inserter is set to, optional.
        /// </summary>
        public Position Drop_position { get; set; }

        /// <summary>
        /// The pickup position the inserter is set to, optional.
        /// </summary>
        public Position Pickup_position { get; set; }

        /// <summary>
        /// Used by Prototype/LogisticContainer, optional.
        /// </summary>
        public List<LogisticFilter> Request_filters { get; set; }

        /// <summary>
        /// Whether this requester chest can request from buffer chests.
        /// </summary>
        public bool? Request_from_buffers { get; set; }

        /// <summary>
        /// Used by Programmable speaker, optional.
        /// </summary>
        public SpeakerParameter Parameters { get; set; }

        /// <summary>
        /// Used by Programmable speaker, optional.
        /// </summary>
        public SpeakerAlertParameter Alert_parameters { get; set; }

        /// <summary>
        /// Used by the rocket silo, optional. Whether auto-launch is enabled.
        /// </summary>
        public bool? Auto_launch { get; set; }

        /// <summary>
        /// Used by Prototype/SimpleEntityWithForce or Prototype/SimpleEntityWithOwner, optional.
        /// </summary>
        public byte? Variation { get; set; }

        /// <summary>
        /// Color of the Prototype/SimpleEntityWithForce, Prototype/SimpleEntityWithOwner, or train station, optional.
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// The name of the train station, optional.
        /// </summary>
        public string Station { get; set; }
    }

    public enum Direction : uint
    {
        Up = 0,
        Right = 2,
        Down = 4,
        Left = 6
    }

    public class Inventory
    {
        /// <summary>
        /// Array of #Item filter objects.
        /// </summary>
        public List<ItemFilter> Filters { get; set; }

        /// <summary>
        /// The index of the first inaccessible item slot due to limiting with the red "bar". 0-based, optional.
        /// </summary>
        public ushort Bar { get; set; }
    }

    public class Schedule
    {
        /// <summary>
        /// Array of #Schedule Record objects.
        /// </summary>
        [JsonPropertyName("schedule")]
        public List<ScheduleRecord> ScheduleRecords { get; set; }

        /// <summary>
        /// Entity numbers of locomotives using this schedule.
        /// </summary>
        public List<int> Locomotives { get; set; }
    }

    public class ScheduleRecord
    {
        /// <summary>
        /// The name of the stop for this schedule record.
        /// </summary>
        public string Station { get; set; }

        /// <summary>
        /// Array of #Wait Condition objects.
        /// </summary>
        public List<WaitCondition> Wait_conditions { get; set; }
    }

    public class WaitCondition
    {
        /// <summary>
        /// One of "time", "inactivity", "full", "empty", "item_count", "circuit", "robots_inactive", "fluid_count", "passenger_present", "passenger_not_present".
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Either "and", or "or". Tells how this condition is to be compared with the preceding conditions in the corresponding wait_conditions array.
        /// </summary>
        public string Compare_type { get; set; }

        /// <summary>
        /// Number of ticks to wait or of inactivity. Only present when type is "time" or "inactivity". Optional.
        /// </summary>
        public uint? Ticks { get; set; }

        /// <summary>
        /// CircuitCondition Object, only present when type is "item_count", "circuit" or "fluid_count".
        /// </summary>
        public CircuitCondition Condition { get; set; }
    }

    public class Tile
    {
        /// <summary>
        /// Prototype name of the tile (e.g. "concrete").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Position of the entity within the blueprint.
        /// </summary>
        public Position Position { get; set; }
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

    public class ItemFilter
    {
        /// <summary>
        /// Name of the item prototype this filter is set to.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Index of the filter, 1-based.
        /// </summary>
        public int Index { get; set; }
    }

    public class InfinitySettings
    {
        /// <summary>
        /// Whether the "remove unfiltered items" checkbox is checked.
        /// </summary>
        public bool Remove_unfiltered_items { get; set; }

        /// <summary>
        /// Filters of the infinity container, optional.
        /// </summary>
        public List<InfinityFilter> Filters { get; set; }
    }

    public class InfinityFilter
    {
        /// <summary>
        /// Name of the item prototype the filter is set to.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Number the filter is set to.
        /// </summary>
        public uint Count { get; set; }

        /// <summary>
        /// Mode of the filter. Either "at-least", "at-most", or "exactly".
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// Index of the filter, 1-based.
        /// </summary>
        public int Index { get; set; }
    }

    public class LogisticFilter
    {
        /// <summary>
        /// Name of the item prototype the filter is set to.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Number the filter is set to. Is 0 for storage chests.
        /// </summary>
        public uint Count { get; set; }

        /// <summary>
        /// Index of the filter, 1-based.
        /// </summary>
        public int Index { get; set; }
    }

    public class SpeakerParameter
    {
        /// <summary>
        /// Volume of the speaker.
        /// </summary>
        public double Playback_volume { get; set; }

        /// <summary>
        /// Whether global playback is enabled.
        /// </summary>
        public bool Playback_globally { get; set; }

        /// <summary>
        /// Boolean, whether polyphony is allowed.
        /// </summary>
        public bool Allow_polyphony { get; set; }
    }

    public class SpeakerAlertParameter
    {
        /// <summary>
        /// Whether an alert is shown.
        /// </summary>
        public bool Show_alert { get; set; }

        /// <summary>
        /// Whether an alert icon is shown on the map.
        /// </summary>
        public bool Show_on_map { get; set; }

        /// <summary>
        /// The icon that is displayed with the alert.
        /// </summary>
        public SignalID Icon_signal_id { get; set; }

        /// <summary>
        /// Message of the alert.
        /// </summary>
        public string Alert_message { get; set; }
    }

    public class Color
    {
        /// <summary>
        /// Red, number from 0 to 1.
        /// </summary>
        public double R { get; set; }

        /// <summary>
        /// Green, number from 0 to 1.
        /// </summary>
        public double G { get; set; }

        /// <summary>
        /// Blue, number from 0 to 1.
        /// </summary>
        public double B { get; set; }

        /// <summary>
        /// Transparency, number from 0 to 1.
        /// </summary>
        public double A { get; set; }
    }

    public class ControlBehavior
    {
        public List<Filter> Filters { get; set; }

        public ArithmeticConditions Arithmetic_conditions { get; set; }

        public DeciderConditions Decider_conditions { get; set; }

        public CircuitCondition Circuit_condition { get; set; }

        public CircuitParameters Circuit_parameters { get; set; }

        public bool? Is_on { get; set; }
        public bool? Use_colors { get; set; }
    }

    public class Filter
    {
        public SignalID Signal { get; set; }

        public int Count { get; set; }

        /// <summary>
        /// Index of the filter, 1-based.
        /// </summary>
        public int Index { get; set; }
    }

    public class ArithmeticConditions
    {
        public SignalID First_signal { get; set; }

        public int? First_constant { get; set; }

        public SignalID Second_signal { get; set; }

        public int? Second_constant { get; set; }

        public string Operation { get; set; }

        public SignalID Output_signal { get; set; }
    }

    public class DeciderConditions
    {
        public SignalID First_signal { get; set; }

        public SignalID Second_signal { get; set; }

        public int? Constant { get; set; }

        public string Comparator { get; set; }

        public SignalID Output_signal { get; set; }

        public bool Copy_count_from_input { get; set; }
    }

    public class CircuitCondition
    {
        public SignalID First_signal { get; set; }

        public SignalID Second_signal { get; set; }

        public int? Constant { get; set; }

        public string Comparator { get; set; }
    }

    public class CircuitParameters
    {
        public bool Signal_value_is_pitch { get; set; }

        public int Instrument_id { get; set; }

        public int Note_id { get; set; }
    }
}
