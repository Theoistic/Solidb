using System;
using System.Collections.Generic;
using System.Linq;

namespace Solidb.Diagnostics
{
    public sealed class SolidCommandLog
    {
        public string Sql { get; }
        public IReadOnlyDictionary<string, object?> Parameters { get; }
        public DateTimeOffset ExecutedAt { get; }

        public SolidCommandLog(string sql, IDictionary<string, object?> parameters)
        {
            Sql = sql;
            Parameters = new Dictionary<string, object?>(parameters);
            ExecutedAt = DateTimeOffset.UtcNow;
        }

        public override string ToString()
        {
            if (Parameters.Count == 0)
                return $"[{ExecutedAt:HH:mm:ss}] {Sql}";

            var paramStr = string.Join(", ", Parameters.Select(kv => $"{kv.Key}={kv.Value}"));
            return $"[{ExecutedAt:HH:mm:ss}] {Sql} -- {paramStr}";
        }
    }

    /// <summary>A simple console-based logger for development.</summary>
    public sealed class ConsoleLogger : ISolidLogger
    {
        public void Log(SolidCommandLog entry) => Console.WriteLine(entry.ToString());
    }
}
