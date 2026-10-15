using System.Text;
using InterviewQuiz.Kernel.Exceptions;

namespace InterviewQuiz.Host.Composition;

public static class ReportCsv
{
    public const int MaxRows = 10_000;

    public const string OverCapMessage = "Export is limited to 10000 rows. Narrow the filters.";

    public static void EnsureWithinCap(int rowCount)
    {
        if (rowCount > MaxRows)
        {
            throw new DomainException(OverCapMessage);
        }
    }

    public static byte[] Write(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        EnsureWithinCap(rows.Count);
        var builder = new StringBuilder();
        builder.Append(string.Join(',', headers.Select(Escape)));
        foreach (var row in rows)
        {
            builder.Append('\n');
            builder.Append(string.Join(',', row.Select(value => Escape(value ?? ""))));
        }

        builder.Append('\n');
        var payload = Encoding.UTF8.GetBytes(builder.ToString());
        var bom = Encoding.UTF8.GetPreamble();
        var bytes = new byte[bom.Length + payload.Length];
        Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
        Buffer.BlockCopy(payload, 0, bytes, bom.Length, payload.Length);
        return bytes;
    }

    public static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
