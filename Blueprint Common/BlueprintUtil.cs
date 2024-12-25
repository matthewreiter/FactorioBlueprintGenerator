using BlueprintCommon.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using zlib;

namespace BlueprintCommon
{
    public static class BlueprintUtil
    {
        public static string ReadBlueprintFileAsJson(string blueprintFile)
        {
            using var inputStream = new StreamReader(blueprintFile);
            var input = inputStream.ReadToEnd().Trim();

            var compressedBytes = Convert.FromBase64String(input.Substring(1));

            var buffer = new MemoryStream();
            using (var output = new ZOutputStream(buffer))
            {
                output.Write(compressedBytes);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        private static void WriteBlueprintFileFromJson(string blueprintFile, string json)
        {
            var buffer = new MemoryStream();
            using (var output = new ZOutputStream(buffer, 9))
            {
                output.Write(Encoding.UTF8.GetBytes(json));
            }

            using var outputWriter = new StreamWriter(blueprintFile);
            outputWriter.Write('0');
            outputWriter.Write(Convert.ToBase64String(buffer.ToArray()));
            outputWriter.Flush();
        }

        public static void WriteOutBlueprint(string blueprintFile, BlueprintWrapper wrapper)
        {
            if (blueprintFile == null)
            {
                return;
            }

            WriteBlueprintFileFromJson(blueprintFile, JsonSerializer.Serialize(wrapper, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreNullValues = true
            }));
        }

        public static void WriteOutJson(string outputJsonFile, object jsonObj)
        {
            if (outputJsonFile == null)
            {
                return;
            }

            using var outputStream = new StreamWriter(outputJsonFile);
            outputStream.Write(JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreNullValues = true
            }));
        }

        public static BlueprintWrapper DeserializeBlueprintWrapper(string json)
        {
            return JsonSerializer.Deserialize<BlueprintWrapper>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        public static void PopulateEntityNumbers(List<Entity> entities)
        {
            var currentEntityNumber = 1;
            var reservedEntityNumbers = entities.Select(entity => entity.Entity_number).ToHashSet();

            int AllocateNextEntityNumber()
            {
                while (reservedEntityNumbers.Contains(currentEntityNumber))
                {
                    currentEntityNumber++;
                }

                return currentEntityNumber++;
            }

            foreach (var entity in entities)
            {
                entity.Entity_number = AllocateNextEntityNumber();
            }
        }

        public static void PopulateIndices(Blueprint blueprint)
        {
            if (blueprint.Icons != null)
            {
                for (int index = 0; index < blueprint.Icons.Count; index++)
                {
                    blueprint.Icons[index].Index = index + 1;
                }
            }

            if (blueprint.Entities != null)
            {
                foreach (var entity in blueprint.Entities)
                {
                    var controlBehavior = entity.Control_behavior;
                    if (controlBehavior != null)
                    {
                        controlBehavior.Sections?.SectionList?.ForEachWithIndex((section, index) =>
                        {
                            section.Index = index + 1;
                            section.Filters?.ForEachWithIndex((filter, filterIndex) => filter.Index = filterIndex + 1);
                        });

                        controlBehavior.Filters?.ForEachWithIndex((filter, index) => filter.Index = index + 1);
                    }
                }
            }
        }

        public static void ForEachWithIndex<T>(this IEnumerable<T> enumerable, Action<T, int> action)
        {
            var index = 0;
            foreach (var item in enumerable)
            {
                action(item, index);
                index++;
            }
        }
    }
}
