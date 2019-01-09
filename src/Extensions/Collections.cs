// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Linq;
using NetMQ;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using System;

namespace Microsoft.Jupyter.Core
{
    public static partial class Extensions
    {

        // NB: This is a polyfill for the equivalent .NET Core 2.0 method, not available in .NET Standard 2.0.
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue @default)
        {
            var success = dict.TryGetValue(key, out var value);
            return success ? value : @default;
        }

        public static bool IsEqual<T>(this T[] actual, T[] expected)
        where T: IEquatable<T>
        {
            return Enumerable
                .Zip(actual, expected, (actualElement, expectedElement) => actualElement.Equals(expectedElement))
                .Aggregate((acc, nextBool) => (acc && nextBool));
        }

        public static IEnumerable<T> AsEnumerable<T>(this Nullable<T> nullable)
        where T : struct
        {
            if (nullable.HasValue)
            {
                yield return nullable.Value;
            }
        }
        public static Dictionary<TKey, TValue> Update<TKey, TValue>(this Dictionary<TKey, TValue> dict, Dictionary<TKey, TValue> other)
        {
            foreach (var item in other)
            {
                dict[item.Key] = item.Value;
            }

            return dict;
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> entry, out TKey key, out TValue value)
        {
            key = entry.Key;
            value = entry.Value;
        }

        public static IEnumerable<TSource> EnumerateInReverse<TSource>(this IList<TSource> source)
        {
            foreach (var idx in Enumerable.Range(1, source.Count))
            {
                yield return source[source.Count - idx];
            }
        }

    }
}