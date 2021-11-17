﻿using System.Diagnostics;
using System.Text.Json.Nodes;

namespace System.Text.Json.Diffs
{
    // https://github.com/benjamine/jsondiffpatch/blob/master/docs/deltas.md
    internal struct JsonDiffDelta
    {
        private readonly JsonDiffOptionsView _options;
        private const int OpTypeDeleted = 0;
        private const int OpTypeTextDiff = 2;
        private const int OpTypeArrayMoved = 3;

        public JsonDiffDelta(in JsonDiffOptionsView options)
            : this(null!, options)
        {
        }

        public JsonDiffDelta(JsonNode delta, in JsonDiffOptionsView options)
        {
            Result = delta;
            _options = options;
        }

        public JsonNode? Result { get; private set; }

        public void Added(JsonNode? newValue)
        {
            EnsureDeltaType(nameof(Added), count: 1);
            var arr = Result!.AsArray();
            arr[0] = newValue.Clone();
        }

        public void Modified(JsonNode? oldValue, JsonNode? newValue)
        {
            EnsureDeltaType(nameof(Modified), count: 2);
            var arr = Result!.AsArray();
            arr[0] = oldValue.Clone();
            arr[1] = newValue.Clone();
        }

        public void Deleted(JsonNode? oldValue)
        {
            EnsureDeltaType(nameof(Deleted), count: 3, opType: OpTypeDeleted);
            var arr = Result!.AsArray();
            arr[0] = oldValue.Clone();
            arr[1] = 0;
        }

        public void ArrayMoveFromDeleted(int newPosition, bool includeOriginalValue)
        {
            EnsureDeltaType(nameof(ArrayMoveFromDeleted), count: 3, opType: OpTypeDeleted);
            var arr = Result!.AsArray();

            if (!includeOriginalValue)
            {
                arr[0] = "";
            }

            arr[1] = newPosition;
            arr[2] = OpTypeArrayMoved;
        }

        public void ArrayChange(int index, bool isLeft, JsonDiffDelta innerChange)
        {
            if (innerChange.Result is null)
            {
                return;
            }

            var result = innerChange.Result;
            Debug.Assert(result.Parent is null);

            if (result.Parent is not null)
            {
                // This can be very slow. We don't want this to happen but
                // in the meantime, we can't fail the operation due to this
                result = result.Clone();
            }

            EnsureDeltaType(nameof(ArrayChange), isArrayChange: true);
            var obj = Result!.AsObject();
            obj.Add(isLeft ? $"_{index:D}" : $"{index:D}", result);
        }

        public void ObjectChange(string propertyName, JsonDiffDelta innerChange)
        {
            if (innerChange.Result is null)
            {
                return;
            }

            var result = innerChange.Result;
            Debug.Assert(result.Parent is null);

            if (result.Parent is not null)
            {
                // This can be very slow. We don't want this to happen but
                // in the meantime, we can't fail the operation due to this
                result = result.Clone();
            }

            EnsureDeltaType(nameof(ObjectChange));
            var obj = Result!.AsObject();
            obj.Add(propertyName, result);
        }

        public void Text(string diff)
        {
            EnsureDeltaType(nameof(Text), count: 3, opType: OpTypeTextDiff);
            var arr = Result!.AsArray();
            arr[0] = diff;
            arr[1] = 0;
            arr[2] = OpTypeTextDiff;
        }

        private void EnsureDeltaType(string opName, int count = 0, int opType = 0,
            bool isArrayChange = false)
        {
            if (count == 0)
            {
                // Object delta, i.e. object and array

                if (Result is null)
                {
                    Result = isArrayChange
                        ? new JsonObject {{"_t", "a"}}
                        : new JsonObject();
                    return;
                }

                if (Result is JsonObject deltaObject)
                {
                    // Check delta object is for array
                    string? deltaType = null;
                    deltaObject.TryGetPropertyValue("_t", out var typeNode);
                    // Perf: this is fine we shouldn't have a node backed by JsonElement here
                    typeNode?.AsValue().TryGetValue(out deltaType);

                    if (string.Equals(deltaType, "a") == isArrayChange)
                    {
                        return;
                    }
                }
            }
            else
            {
                // Value delta
                if (Result is null)
                {
                    var newDeltaArray = new JsonArray();
                    for (var i = 0; i < count; i++)
                    {
                        if (i == 2)
                        {
                            newDeltaArray.Add(opType);
                        }
                        else
                        {
                            newDeltaArray.Add(null);
                        }
                    }

                    Result = newDeltaArray;
                    return;
                }

                if (Result is JsonArray deltaArray && deltaArray.Count == count)
                {
                    if (count < 3)
                    {
                        return;
                    }

                    if (deltaArray[count - 1]?.AsValue().GetValue<int>() == opType)
                    {
                        return;
                    }
                }
            }

            throw new InvalidOperationException(
                $"Operation '{opName}' cannot be performed on current delta result.");
        }

        public static JsonDiffDelta CreateAdded(JsonNode? newValue)
        {
            var delta = new JsonDiffDelta();
            delta.Added(newValue);
            return delta;
        }

        public static JsonDiffDelta CreateDeleted(JsonNode? oldValue)
        {
            var delta = new JsonDiffDelta();
            delta.Deleted(oldValue);
            return delta;
        }

        public static void ChangeDeletedToArrayMoved(JsonDiffDelta delta, int index,
            int newPosition, bool includeOriginalValue)
        {
            if (delta.Result is not JsonObject obj)
            {
                return;
            }

            if (!obj.TryGetPropertyValue($"_{index:D}", out var itemDelta)
                || itemDelta is null)
            {
                return;
            }

            var newItemDelta = new JsonDiffDelta(itemDelta, delta._options);
            newItemDelta.ArrayMoveFromDeleted(newPosition, includeOriginalValue);
        }
    }
}