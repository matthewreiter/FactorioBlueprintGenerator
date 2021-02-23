using BlueprintCommon.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueprintGenerator
{
    public static class ConnectionUtil
    {
        public static Dictionary<string, ConnectionPoint> CreateConnections(params ConnectionPoint[] connectionPoints)
        {
            return connectionPoints
                .Select((connectionPoint, index) => new { Key = (index + 1).ToString(), Value = connectionPoint })
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public static void AddConnection(CircuitColor color, Entity firstEntity, CircuitId? firstCircuitId, Entity secondEntity, CircuitId? secondCircuitId)
        {
            void AddOneWayConnection(Entity startEntity, CircuitId? startCircuitId, Entity endEntity, CircuitId? endCircuitId)
            {
                var connections = startEntity.Connections;
                if (connections == null)
                {
                    connections = new Dictionary<string, ConnectionPoint>();
                    startEntity.Connections = connections;
                }

                var connectionPointKey = startCircuitId switch
                {
                    CircuitId.Input or null => "1",
                    CircuitId.Output => "2",
                    _ => throw new ArgumentException($"Invalid circuit ID: {startCircuitId}")
                };

                if (!connections.TryGetValue(connectionPointKey, out var connectionPoint))
                {
                    connectionPoint = new ConnectionPoint();
                    connections.Add(connectionPointKey, connectionPoint);
                }

                List<ConnectionData> connectionData;
                switch (color)
                {
                    case CircuitColor.Red:
                        connectionData = connectionPoint.Red;
                        if (connectionData == null)
                        {
                            connectionData = new List<ConnectionData>();
                            connectionPoint.Red = connectionData;
                        }

                        break;

                    case CircuitColor.Green:
                        connectionData = connectionPoint.Green;
                        if (connectionData == null)
                        {
                            connectionData = new List<ConnectionData>();
                            connectionPoint.Green = connectionData;
                        }

                        break;
                    default:
                        throw new ArgumentException($"Invalid circuit color: {color}", nameof(color));
                }

                connectionData.Add(new ConnectionData
                {
                    Entity_id = endEntity.Entity_number,
                    Circuit_id = endCircuitId
                });
            }

            AddOneWayConnection(firstEntity, firstCircuitId, secondEntity, secondCircuitId);
            AddOneWayConnection(secondEntity, secondCircuitId, firstEntity, firstCircuitId);
        }

        public static void AddNeighbor(Entity firstEntity, Entity secondEntity)
        {
            static void AddOneWayNeighbor(Entity startEntity, Entity endEntity)
            {
                var neighbors = startEntity.Neighbors;
                if (neighbors == null)
                {
                    neighbors = new List<int>();
                    startEntity.Neighbors = neighbors;
                }

                neighbors.Add(endEntity.Entity_number);
            }

            AddOneWayNeighbor(firstEntity, secondEntity);
            AddOneWayNeighbor(secondEntity, firstEntity);
        }

        public enum CircuitColor
        {
            Red,
            Green
        }
    }
}
