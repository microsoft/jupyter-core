using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Data;

namespace Microsoft.Jupyter.Core
{
    internal static class Extensions
    {
        internal static List<string> GetFieldNames(this IDataReader reader)
        {
            return Enumerable.Range(0, reader.FieldCount)
                             .Select(idx => reader.GetName(idx))
                             .ToList();
        }

        internal static List<object[]> ReadAllRows(this IDataReader reader)
        {
            var nFields = reader.GetFieldNames().Count;
            var values = new object[nFields];
            var rows = new List<object[]>();

            while (reader.Read())
            {
                reader.GetValues(values);
                rows.Add(values.ToArray());
            }

            return rows;
        }
    }
}
