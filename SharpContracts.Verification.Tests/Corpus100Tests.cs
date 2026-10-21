using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SharpContracts.Verification.Tests;

/// <summary>
/// 100 archived concrete CLR regression vectors; not formal Z3 proofs.
/// The fixture is gzip-compressed JSON copied into test output unchanged.
/// </summary>
public sealed class Corpus100Tests
{
    private sealed record Vector(int Iteration, string Kind, string[] Args, JsonElement Expect);

    private static IReadOnlyList<Vector> Read()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "corpus100.json.gz.b64");
        byte[] zipped = Convert.FromBase64String(File.ReadAllText(path).Trim());
        using var input = new MemoryStream(zipped);
        using var unzip = new GZipStream(input, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(unzip);
        return doc.RootElement.EnumerateArray().Select(row =>
            new Vector(row.GetProperty("iteration").GetInt32(),
                row.GetProperty("kind").GetString()!,
                row.GetProperty("args").EnumerateArray().Select(x => x.GetString()!).ToArray(),
                row.GetProperty("expect").Clone())).ToList();
    }

    public static IEnumerable<object[]> Cases() => Read().Select(v =>
        new object[] { v.Iteration, v.Kind, v.Args, v.Expect });

    private static uint U(string value) => uint.Parse(value, CultureInfo.InvariantCulture);
    private static bool B(JsonElement e, string property) => e.GetProperty(property).GetBoolean();
    private static uint N(JsonElement e, string property) => U(e.GetProperty(property).GetString()!);

    [Theory]
    [MemberData(nameof(Cases))]
    public void ConcreteVectorMatchesArchive(int iteration, string kind, string[] args, JsonElement expected)
    {
        Assert.InRange(iteration, 1, 100);
        uint a = U(args[0]);
        switch (kind)
        {
            case "payment":
                uint badSplit = unchecked(a / 2u + a / 2u);
                uint first = a / 2u, second = a - first;
                Assert.Equal(B(expected, "bad.ok"), badSplit == a);
                Assert.Equal(B(expected, "good.ok"), unchecked(first + second) == a);
                break;
            case "buffer":
                uint incoming = U(args[1]), capacity = U(args[2]);
                Assert.True(a <= capacity);
                bool badAdmission = unchecked(a + incoming) <= capacity;
                bool safeAdmission = incoming <= capacity - a;
                Assert.Equal(B(expected, "bugAdmits"), badAdmission);
                Assert.Equal(B(expected, "correctAdmits"), safeAdmission);
                Assert.Equal(B(expected, "unsafeAdmission"), badAdmission && !safeAdmission);
                break;
            case "midpoint":
                uint high = U(args[1]);
                Assert.True(a <= high);
                uint badMid = unchecked(a + high) / 2u;
                uint goodMid = unchecked(a + (high - a) / 2u);
                Assert.Equal(N(expected, "bad"), badMid);
                Assert.Equal(N(expected, "good"), goodMid);
                Assert.Equal(B(expected, "badInside"), a <= badMid && badMid <= high);
                Assert.Equal(B(expected, "goodInside"), a <= goodMid && goodMid <= high);
                break;
            case "counter":
                uint incremented = unchecked(a + 1u);
                Assert.Equal(N(expected, "bad"), incremented);
                Assert.Equal(B(expected, "wrapped"), incremented < a);
                Assert.Equal(B(expected, "canIncrement"), a < uint.MaxValue);
                break;
            case "headroom":
                uint requested = U(args[1]), limit = U(args[2]);
                Assert.True(a <= limit);
                bool badHeadroom = unchecked(a + requested) <= limit;
                bool goodHeadroom = requested <= limit - a;
                Assert.Equal(B(expected, "badAdmits"), badHeadroom);
                Assert.Equal(B(expected, "goodAdmits"), goodHeadroom);
                Assert.Equal(B(expected, "wrapped"), requested > uint.MaxValue - a);
                break;
            default: throw new InvalidOperationException("Unknown corpus kind: " + kind);
        }
    }

    [Fact]
    public void CorpusContainsExactlyOneHundredVectors()
    {
        var rows = Read();
        Assert.Equal(100, rows.Count);
        Assert.Equal(Enumerable.Range(1, 100), rows.Select(r => r.Iteration).OrderBy(i => i));
        foreach (string kind in new[] { "payment", "buffer", "midpoint", "counter", "headroom" })
            Assert.Equal(20, rows.Count(r => r.Kind == kind));
    }
}
