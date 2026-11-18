using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using FishUI;
using FishUI.Controls;

namespace RusEfiTools.Gui;

internal sealed record LoadedLog(TelemetryChannel[] Channels, int Samples, int Fields, int Markers, SensorLogData Sensors);

internal static class MlgLogReader
{
    // EFI Analytics MLVLG v2: 24-byte header, 89-byte field descriptors,
    // big-endian records and an additive payload checksum.
    public static LoadedLog Load(string path, CancellationToken cancellation = default, IEnumerable<string>? channelNames = null)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.SequentialScan);
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = new ZipArchive(file, ZipArchiveMode.Read);
            var entries = zip.Entries.Where(entry => entry.FullName.EndsWith(".mlg", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (entries.Length != 1)
            {
                throw new InvalidDataException("The ZIP must contain exactly one .mlg log.");
            }
            using var stream = entries[0].Open();
            return Read(stream, cancellation, channelNames);
        }
        if (!Path.GetExtension(path).Equals(".mlg", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Drop an .mlg file or a ZIP containing one .mlg file.");
        }
        return Read(file, cancellation, channelNames);
    }

    internal static LoadedLog Read(Stream stream, CancellationToken cancellation = default, IEnumerable<string>? channelNames = null)
    {
        byte[] header = new byte[24];
        stream.ReadExactly(header);
        if (!header.AsSpan(0, 6).SequenceEqual("MLVLG\0"u8) || BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(6)) != 2)
        {
            throw new InvalidDataException("Only binary MLVLG version 2 logs are supported.");
        }
        uint dataStart = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16));
        int recordSize = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(20));
        int count = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(22));
        long descriptorEnd = 24L + count * 89;
        if (count == 0 || recordSize == 0 || dataStart < descriptorEnd || dataStart > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Invalid MLG header lengths.");
        }

        var fields = new List<Field>();
        byte[] descriptor = new byte[89];
        int offset = 0;
        for (int index = 0; index < count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            stream.ReadExactly(descriptor);
            byte type = descriptor[0];
            int size = type switch
            {
                0 or 1 or 10 => 1,
                2 or 3 or 11 => 2,
                4 or 5 or 7 or 12 => 4,
                6 => 8,
                _ => throw new InvalidDataException($"Unsupported MLG field type {type}.")
            };
            fields.Add(new Field(Text(descriptor.AsSpan(1, 34)), Text(descriptor.AsSpan(35, 10)),
                type, offset, Float(descriptor.AsSpan(46)), Float(descriptor.AsSpan(50)), descriptor[45]));
            offset += size;
        }
        if (offset != recordSize)
        {
            throw new InvalidDataException("MLG field sizes do not match the record length.");
        }

        byte[] skipBuffer = new byte[8192];
        for (long remaining = dataStart - descriptorEnd; remaining > 0;)
        {
            cancellation.ThrowIfCancellationRequested();
            int length = (int)Math.Min(remaining, skipBuffer.Length);
            stream.ReadExactly(skipBuffer.AsSpan(0, length));
            remaining -= length;
        }

        Field Find(string name) => fields.FirstOrDefault(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Required channel '{name}' was not found.");
        var timeField = Find("Time");
        if (timeField.Units != "s")
        {
            throw new InvalidDataException("Expected Time in seconds.");
        }
        var names = (channelNames ?? ["MAP", "RPM", "TPS"]).Distinct(StringComparer.OrdinalIgnoreCase);
        var selected = names.Select(name => fields.FirstOrDefault(field =>
                field.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Where(field => field != null).Cast<Field>().ToArray();
        FishColor[] colors = [new(65, 145, 255), new(255, 78, 78), new(100, 235, 55), new(255, 200, 87)];
        var series = selected.Select((field, index) => new LineChartSeries($"{field.Name} ({field.Units})",
            colors[field.Name.ToUpperInvariant() switch { "MAP" => 0, "RPM" => 1, "TPS" => 2, _ => index % colors.Length }])
        {
            MaxPoints = 0
        }).ToArray();

        byte[] record = new byte[recordSize + 4];
        var sensors = new SensorLogData(fields.ToArray(), recordSize);
        byte[] marker = new byte[53];
        double? firstTime = null;
        float previousTime = -1;
        int samples = 0;
        int markers = 0;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            int type = stream.ReadByte();
            if (type == -1)
            {
                break;
            }
            if (type == 1)
            {
                stream.ReadExactly(marker);
                markers++;
                continue;
            }
            if (type != 0)
            {
                throw new InvalidDataException($"Unknown record type {type} after sample {samples}.");
            }
            stream.ReadExactly(record);
            var payload = record.AsSpan(3, recordSize);
            int checksum = 0;
            foreach (byte value in payload)
            {
                checksum += value;
            }
            if ((byte)checksum != record[^1])
            {
                throw new InvalidDataException($"Checksum failed at sample {samples + 1}.");
            }
            double rawTime = timeField.Read(payload);
            if (!double.IsFinite(rawTime))
            {
                throw new InvalidDataException("The log contains an invalid timestamp.");
            }
            firstTime ??= rawTime;
            float time = (float)(rawTime - firstTime.Value);
            if (time < previousTime)
            {
                throw new InvalidDataException("The log's Time channel runs backwards.");
            }
            for (int index = 0; index < selected.Length; index++)
            {
                float value = (float)selected[index].Read(payload);
                if (!float.IsFinite(value))
                {
                    throw new InvalidDataException($"Invalid value in {selected[index].Name} at sample {samples + 1}.");
                }
                var point = new Vector2(time, value);
                if (time == previousTime)
                {
                    series[index].Points[^1] = point;
                }
                else
                {
                    series[index].Points.Add(point);
                }
            }
            sensors.AddSample(time, payload);
            previousTime = time;
            samples++;
        }
        if (samples < 2 || previousTime <= 0)
        {
            throw new InvalidDataException("The log needs at least two samples with increasing time.");
        }
        var channels = series.Select((data, index) => new TelemetryChannel(data,
            Math.Max(selected[index].Name.Equals("RPM", StringComparison.OrdinalIgnoreCase) ? 8000 : 200,
                MathF.Ceiling(data.Points.Max(point => point.Y) / 100) * 100),
            selected[index].Name.Equals("RPM", StringComparison.OrdinalIgnoreCase)) { Name = selected[index].Name }).ToArray();
        sensors.Seal();
        return new LoadedLog(channels, samples, count, markers, sensors);
    }

    private static float Float(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadSingleBigEndian(bytes);

    private static string Text(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]);
    }

    internal sealed record Field(string Name, string Units, byte Type, int Offset, float Scale, float Transform, byte DisplayStyle = 0)
    {
        public double Read(ReadOnlySpan<byte> payload)
        {
            var bytes = payload[Offset..];
            double raw = Type switch
            {
                0 or 10 => bytes[0],
                1 => (sbyte)bytes[0],
                2 or 11 => BinaryPrimitives.ReadUInt16BigEndian(bytes),
                3 => BinaryPrimitives.ReadInt16BigEndian(bytes),
                4 or 12 => BinaryPrimitives.ReadUInt32BigEndian(bytes),
                5 => BinaryPrimitives.ReadInt32BigEndian(bytes),
                6 => BinaryPrimitives.ReadInt64BigEndian(bytes),
                7 => Float(bytes),
                _ => throw new InvalidDataException($"Unsupported scalar channel type {Type}.")
            };
            return Type >= 10 ? raw : (raw + Transform) * Scale;
        }
    }
}
