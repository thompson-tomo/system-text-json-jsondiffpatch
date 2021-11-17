﻿using System.Diagnostics;
using System.IO;
using System.Text.Json.Diffs;
using System.Text.Json.Nodes;

namespace System.Text.Json
{
    /// <summary>
    /// Provides methods to diff and patch JSON objects.
    /// </summary>
    public static partial class JsonDiffPatcher
    {
        /// <summary>
        /// Compares two JSON objects and generates a diff document.
        /// </summary>
        /// <param name="leftJson">The left object.</param>
        /// <param name="rightJson">The right object.</param>
        /// <param name="options">The diffing options.</param>
        /// <param name="serializerOptions">The serializer options.</param>
        public static JsonDocument? Diff(ReadOnlySpan<byte> leftJson, ReadOnlySpan<byte> rightJson,
            JsonDiffOptions options = default, JsonSerializerOptions? serializerOptions = null)
        {
            var root = DiffInternal(JsonNode.Parse(leftJson), JsonNode.Parse(rightJson),
                new JsonDiffOptionsView(options, false));
            return WriteDocument(root, serializerOptions);
        }

        /// <summary>
        /// Compares two JSON objects and generates a diff document.
        /// </summary>
        /// <param name="leftJson">The left object.</param>
        /// <param name="rightJson">The right object.</param>
        /// <param name="options">The diffing options.</param>
        /// <param name="serializerOptions">The serializer options.</param>
        public static JsonDocument? Diff(Stream leftJson, Stream rightJson,
            JsonDiffOptions options = default, JsonSerializerOptions? serializerOptions = null)
        {
            _ = leftJson ?? throw new ArgumentNullException(nameof(leftJson));
            _ = rightJson ?? throw new ArgumentNullException(nameof(rightJson));

            var root = DiffInternal(JsonNode.Parse(leftJson), JsonNode.Parse(rightJson),
                new JsonDiffOptionsView(options, false));
            return WriteDocument(root, serializerOptions);
        }

        /// <summary>
        /// Compares two JSON objects and generates a diff document.
        /// </summary>
        /// <param name="leftJson">The left object.</param>
        /// <param name="rightJson">The right object.</param>
        /// <param name="options">The diffing options.</param>
        /// <param name="serializerOptions">The serializer options.</param>
        public static JsonDocument? Diff(ref Utf8JsonReader leftJson, ref Utf8JsonReader rightJson,
            JsonDiffOptions options = default, JsonSerializerOptions? serializerOptions = null)
        {
            var root = DiffInternal(JsonNode.Parse(ref leftJson), JsonNode.Parse(ref rightJson),
                new JsonDiffOptionsView(options, false));
            return WriteDocument(root, serializerOptions);
        }

        /// <summary>
        /// Compares two JSON objects and generates a diff document.
        /// </summary>
        /// <param name="leftJson">The left object.</param>
        /// <param name="rightJson">The right object.</param>
        /// <param name="options">The diffing options.</param>
        /// <param name="serializerOptions">The serializer options.</param>
        public static JsonDocument? Diff(string? leftJson, string? rightJson,
            JsonDiffOptions options = default, JsonSerializerOptions? serializerOptions = null)
        {
            var root = DiffInternal(leftJson is null ? null : JsonNode.Parse(leftJson),
                rightJson is null ? null : JsonNode.Parse(rightJson),
                new JsonDiffOptionsView(options, false));
            return WriteDocument(root, serializerOptions);
        }

        /// <summary>
        /// Compares two JSON objects and generates a mutable diff document.
        /// </summary>
        /// <param name="node">The left object.</param>
        /// <param name="another">The right object.</param>
        /// <param name="options">The diffing options.</param>
        public static JsonNode? Diff(this JsonNode? node, JsonNode? another, JsonDiffOptions options = default)
        {
            return DiffInternal(node, another, new JsonDiffOptionsView(options, true));
        }

        /// <summary>
        /// Compares two JSON objects from files and generates a diff document.
        /// </summary>
        /// <param name="leftFilePath">The path to the file containing left object.</param>
        /// <param name="rightFilePath">The path to the file containing left object.</param>
        /// <param name="options">The diffing options.</param>
        /// <param name="serializerOptions">The serializer options.</param>
        public static JsonDocument? DiffFile(string leftFilePath, string rightFilePath,
            JsonDiffOptions options = default, JsonSerializerOptions? serializerOptions = null)
        {
            _ = leftFilePath ?? throw new ArgumentNullException(nameof(leftFilePath));
            _ = rightFilePath ?? throw new ArgumentNullException(nameof(rightFilePath));

            using var fsLeft = File.OpenRead(leftFilePath);
            using var fsRight = File.OpenRead(rightFilePath);

            return Diff(fsLeft, fsRight, options, serializerOptions);
        }

        private static JsonNode? DiffInternal(JsonNode? left, JsonNode? right, in JsonDiffOptionsView options)
        {
            var delta = new JsonDiffDelta(options);

            left ??= "";
            right ??= "";

            // Compare two objects
            if (left is JsonObject leftObj && right is JsonObject rightObj)
            {
                DiffObject(ref delta, leftObj, rightObj, options);
                return delta.Result;
            }

            // For long texts
            // Compare two long texts
            if (IsLongText(left, right, options, out var leftText, out var rightText))
            {
                DiffLongText(ref delta, leftText!, rightText!, options);
                return delta.Result;
            }

            // Compare two arrays
            if (left is JsonArray leftArr && right is JsonArray rightArr)
            {
                DiffArray(ref delta, leftArr, rightArr, options);
                return delta.Result;
            }

            // None of the above methods returned a result, fallback to check if both values are deeply equal
            // This should also handle DateTime and other CLR types that are strings in JSON
            if (!left.DeepEquals(right))
            {
                delta.Modified(left, right);
                return delta.Result;
            }

            Debug.Assert(delta.Result is null);
            return null;
        }

        private static JsonDocument? WriteDocument(JsonNode? node, JsonSerializerOptions? serializerOptions)
        {
            if (node is null)
            {
                return null;
            }

            using var buffer = JsonBytes.FromNode(node, serializerOptions);
            var reader = buffer.GetReader();
            return JsonDocument.ParseValue(ref reader);
        }
    }
}