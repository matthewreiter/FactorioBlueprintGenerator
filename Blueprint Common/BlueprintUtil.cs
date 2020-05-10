using BlueprintCommon.Models;
using System;
using System.IO;
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

        public static void PopulateIndices(Blueprint blueprint)
        {
            if (blueprint.Icons != null)
            {
                for (int index = 0; index < blueprint.Icons.Count; index++)
                {
                    blueprint.Icons[index].Index = index + 1;
                }
            }
        }
    }
}
