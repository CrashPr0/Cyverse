using System;
using System.Collections.Generic;

namespace Cyverse.Forensics
{
    /// <summary>Result of running a mini-KQL query: an error, a scalar
    /// (count), or a row set.</summary>
    public class QueryResult
    {
        public string error;
        public bool isScalar;
        public string scalar;
        public string[] headers;
        public List<string[]> rows;
    }

    /// <summary>
    /// A deliberately small KQL-style query engine, KC7-style:
    ///
    ///   TableName
    ///     | where column == "value"     (also != and contains)
    ///     | project col1, col2
    ///     | distinct column
    ///     | take N
    ///     | count
    ///
    /// Everything is case-insensitive; string values may be quoted (needed
    /// when they contain spaces). Pure C# — no Unity types — so it's easy to
    /// unit-test and reuse.
    /// </summary>
    public static class MiniKql
    {
        public static QueryResult Run(LogDatabase db, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Err("Empty query. Try:  Employees | count");

            string[] segments = query.Split('|');
            var table = db.Find(segments[0].Trim());
            if (table == null)
                return Err($"Unknown table '{segments[0].Trim()}'. Type  tables  to list them.");

            var headers = (string[])table.columns.Clone();
            var rows = new List<string[]>(table.rows);

            for (int s = 1; s < segments.Length; s++)
            {
                var tokens = Tokenize(segments[s]);
                if (tokens.Count == 0) return Err("Empty pipe segment — remove the trailing '|'.");
                string op = tokens[0].ToLowerInvariant();

                switch (op)
                {
                    case "where":
                    {
                        if (tokens.Count != 4)
                            return Err("where needs:  where <column> ==|!=|contains <value>");
                        int col = IndexOf(headers, tokens[1]);
                        if (col < 0) return ErrColumn(tokens[1], headers);
                        string cmp = tokens[2].ToLowerInvariant();
                        string val = tokens[3];
                        Func<string, bool> pass;
                        if (cmp == "==") pass = c => string.Equals(c, val, StringComparison.OrdinalIgnoreCase);
                        else if (cmp == "!=") pass = c => !string.Equals(c, val, StringComparison.OrdinalIgnoreCase);
                        else if (cmp == "contains") pass = c => c.IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0;
                        else return Err($"Unknown comparison '{tokens[2]}'. Use ==, != or contains.");
                        rows = rows.FindAll(r => pass(r[col]));
                        break;
                    }
                    case "count":
                    {
                        if (s != segments.Length - 1)
                            return Err("count must be the last step of the query.");
                        return new QueryResult { isScalar = true, scalar = rows.Count.ToString() };
                    }
                    case "take":
                    {
                        if (tokens.Count != 2 || !int.TryParse(tokens[1], out int n) || n < 1)
                            return Err("take needs a number, e.g.  | take 5");
                        if (rows.Count > n) rows = rows.GetRange(0, n);
                        break;
                    }
                    case "distinct":
                    {
                        if (tokens.Count != 2) return Err("distinct needs one column, e.g.  | distinct sender");
                        int col = IndexOf(headers, tokens[1]);
                        if (col < 0) return ErrColumn(tokens[1], headers);
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var outRows = new List<string[]>();
                        foreach (var r in rows)
                            if (seen.Add(r[col])) outRows.Add(new[] { r[col] });
                        headers = new[] { headers[col] };
                        rows = outRows;
                        break;
                    }
                    case "project":
                    {
                        if (tokens.Count < 2) return Err("project needs columns, e.g.  | project sender, subject");
                        var wanted = new List<int>();
                        var newHeaders = new List<string>();
                        for (int i = 1; i < tokens.Count; i++)
                        {
                            string name = tokens[i].TrimEnd(',');
                            if (name.Length == 0) continue;
                            int col = IndexOf(headers, name);
                            if (col < 0) return ErrColumn(name, headers);
                            wanted.Add(col);
                            newHeaders.Add(headers[col]);
                        }
                        var projected = new List<string[]>(rows.Count);
                        foreach (var r in rows)
                        {
                            var row = new string[wanted.Count];
                            for (int i = 0; i < wanted.Count; i++) row[i] = r[wanted[i]];
                            projected.Add(row);
                        }
                        headers = newHeaders.ToArray();
                        rows = projected;
                        break;
                    }
                    default:
                        return Err($"Unknown operator '{tokens[0]}'. Operators: where, project, distinct, take, count.");
                }
            }

            return new QueryResult { headers = headers, rows = rows };
        }

        /// <summary>Whitespace tokenizer that keeps quoted strings ("a b")
        /// together and strips their quotes. Commas survive inside tokens so
        /// project lists still split naturally on spaces.</summary>
        private static List<string> Tokenize(string segment)
        {
            var tokens = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            foreach (char c in segment)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0) { tokens.Add(current.ToString()); current.Length = 0; }
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0) tokens.Add(current.ToString());
            return tokens;
        }

        private static int IndexOf(string[] headers, string col)
        {
            col = col.TrimEnd(',');
            for (int i = 0; i < headers.Length; i++)
                if (string.Equals(headers[i], col, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private static QueryResult Err(string message) => new QueryResult { error = message };

        private static QueryResult ErrColumn(string col, string[] headers) =>
            Err($"Unknown column '{col}'. Columns here: {string.Join(", ", headers)}");
    }
}
