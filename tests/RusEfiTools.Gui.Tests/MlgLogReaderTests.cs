using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Xunit;

namespace RusEfiTools.Gui.Tests;

public sealed class MlgLogReaderTests
{
    [Fact]
    public void ReadsBigEndianScalingMarkersAndNormalizesTime()
    {
        using var stream = Fixture();
        var log = MlgLogReader.Read(stream, TestContext.Current.CancellationToken);
        Assert.Equal(2, log.Samples);
        Assert.Equal(1, log.Markers);
        Assert.Equal(new Vector2(0, 100), log.Channels[0].Series.Points[0]);
        Assert.Equal(new Vector2(1, 3000), log.Channels[1].Series.Points[1]);
        Assert.Equal(50, log.Channels[2].Series.Points[0].Y);
        Assert.Equal(4, log.Sensors.Fields.Count);
        Assert.Equal(100, log.Sensors.ValueAt(0, 0));
        Assert.Equal(3000, log.Sensors.ValueAt(log.Sensors.FindNearest(0.8f), 2));
    }

    [Fact]
    public void RejectsBadChecksumAndTruncatedRecord()
    {
        byte[] bytes = Fixture().ToArray();
        bytes[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => MlgLogReader.Read(new MemoryStream(bytes), TestContext.Current.CancellationToken));
        Assert.Throws<EndOfStreamException>(() => MlgLogReader.Read(new MemoryStream(bytes[..^2]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RejectsUnsupportedHeaderAndHonorsCancellation()
    {
        byte[] bytes = Fixture().ToArray();
        bytes[7] = 1;
        Assert.Throws<InvalidDataException>(() => MlgLogReader.Read(new MemoryStream(bytes), TestContext.Current.CancellationToken));
        Assert.Throws<OperationCanceledException>(() => MlgLogReader.Read(Fixture(), new CancellationToken(true)));
    }

    [Fact]
    public void DisplayReductionPreservesExtremaAndRawCursorValues()
    {
        var points = Enumerable.Range(0, 1000).Select(i => new Vector2(i / 1000f, i == 501 ? 100 : 0)).ToList();
        var reduced = GraphSamples.ForDisplay(points, 0, 1, 10).ToArray();
        Assert.Contains(points[501], reduced);
        Assert.True(reduced.Length <= 40);
        Assert.Equal(100, GraphSamples.ValueAt(points, points[501].X));
        Assert.Equal(1000, points.Count);
    }

    [Fact]
    public void OptionalLocalLogsLoadWithoutChangingSourceFiles()
    {
        string? directory = Environment.GetEnvironmentVariable("RUSEFI_TEST_LOGS");
        if (directory == null)
        {
            return;
        }
        foreach (string path in Directory.EnumerateFiles(directory).Where(path =>
            path.EndsWith(".mlg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            var before = File.GetLastWriteTimeUtc(path);
            var log = MlgLogReader.Load(path, TestContext.Current.CancellationToken);
            Assert.True(log.Samples > 2, path);
            Assert.Equal(1941, log.Fields);
            Assert.Equal(0, log.Channels[0].Series.Points[0].X);
            Assert.Equal(before, File.GetLastWriteTimeUtc(path));
            Console.WriteLine($"{Path.GetFileName(path)}: {log.Samples} samples, {log.Channels[0].Series.Points[^1].X:F2} seconds");
        }
    }

    [Fact]
    public void LoadsConfiguredChannelsWithoutRequiringDefaultNames()
    {
        using var stream = Fixture("Air: Flow estimate");
        var log = MlgLogReader.Read(stream, TestContext.Current.CancellationToken,
            ["air: flow estimate", "Missing", "AIR: FLOW ESTIMATE", "TPS"]);
        Assert.Equal(["Air: Flow estimate", "TPS"], log.Channels.Select(channel => channel.Name));
        Assert.Equal(3000, log.Channels[0].Series.Points[1].Y);
        Assert.Equal(4, log.Sensors.Fields.Count);
        Assert.Equal(1, log.Sensors.Duration);
    }

    [Fact]
    public void MissingConfiguredChannelsStillLoadInspectorDataAndTimeline()
    {
        using var stream = Fixture();
        var log = MlgLogReader.Read(stream, TestContext.Current.CancellationToken, ["Missing"]);
        Assert.Empty(log.Channels);
        Assert.Equal(4, log.Sensors.Fields.Count);
        Assert.Equal(1, log.Sensors.Duration);
    }

    private static MemoryStream Fixture(string rpmName = "RPM")
    {
        var stream = new MemoryStream();
        byte[] header = new byte[24];
        "MLVLG\0"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), 2);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), 24 + 4 * 89);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(20), 10);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(22), 4);
        stream.Write(header);
        WriteField("Time", "s", 7, 1, 0);
        WriteField("MAP", "kPa", 2, 0.5f, 10);
        WriteField(rpmName, "RPM", 2, 1, 0);
        WriteField("TPS", "%", 3, 0.5f, 0);
        for (int row = 0; row < 2; row++)
        {
            byte[] record = new byte[15];
            BinaryPrimitives.WriteSingleBigEndian(record.AsSpan(4), 100 + row);
            BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(8), 190);
            BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(10), (ushort)(2000 + row * 1000));
            BinaryPrimitives.WriteInt16BigEndian(record.AsSpan(12), 100);
            record[^1] = (byte)record.Skip(4).Take(10).Sum(value => value);
            stream.Write(record);
            if (row == 0)
            {
                byte[] marker = new byte[54];
                marker[0] = 1;
                stream.Write(marker);
            }
        }
        stream.Position = 0;
        return stream;

        void WriteField(string name, string units, byte type, float scale, float transform)
        {
            byte[] field = new byte[89];
            field[0] = type;
            Encoding.ASCII.GetBytes(name).CopyTo(field, 1);
            Encoding.ASCII.GetBytes(units).CopyTo(field, 35);
            BinaryPrimitives.WriteSingleBigEndian(field.AsSpan(46), scale);
            BinaryPrimitives.WriteSingleBigEndian(field.AsSpan(50), transform);
            stream.Write(field);
        }
    }
}
