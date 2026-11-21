using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ElectroInventory.Wire
{
    internal sealed class Entry
    {
        public string Id, Name, Category, Description, Detail;
        public double Quantity = double.NaN;
        public double Price = double.NaN;
        public byte Group; // 0 player/items, 1 stored items, 2 weapons, 3 cash, 4 buy, 5 sell
    }

    internal sealed class Snapshot
    {
        public Guid Session;
        public long PublicationStamp;
        public int Character;
        public byte Mode; // 0 inventory, 1 storage, 2 trade
        public bool BlocksInput, Incomplete;
        public string Title;
        public double Balance = double.NaN;
        public List<Entry> Rows = new List<Entry>();
    }

    internal static class Protocol
    {
        public const int Magic = 0x45494E31, Version = 1, Header = 24, Capacity = 1024 * 1024;
        public const int MaxRows = 2000, MaxTextBytes = 4096;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static string Channel(int pid) => @"Local\ElectroInventory.Lsr.read.v1." + pid;
        public static bool Fresh(long stamp, long now) =>
            stamp > 0 && stamp <= now && now - stamp <= Stopwatch.Frequency * 2;

        public static byte[] Encode(Snapshot value)
        {
            using (var rows = new MemoryStream())
            using (var rowWriter = new BinaryWriter(rows, Utf8, true))
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output, Utf8, true))
            {
                int count = 0;
                foreach (var row in value.Rows)
                {
                    if (count == MaxRows || rows.Length > Capacity - Header - 6 * MaxTextBytes - 128)
                        break;
                    rowWriter.Write(row.Group);
                    rowWriter.Write(row.Quantity);
                    rowWriter.Write(row.Price);
                    WriteText(rowWriter, row.Id);
                    WriteText(rowWriter, row.Name);
                    WriteText(rowWriter, row.Category);
                    WriteText(rowWriter, row.Description);
                    WriteText(rowWriter, row.Detail);
                    count++;
                }
                writer.Write(value.Session.ToByteArray());
                writer.Write(value.Character);
                writer.Write(value.Mode);
                writer.Write(value.BlocksInput);
                writer.Write(value.Incomplete || count < value.Rows.Count);
                writer.Write(value.Balance);
                WriteText(writer, value.Title);
                writer.Write(count);
                rows.WriteTo(output);
                return output.ToArray();
            }
        }

        public static Snapshot Decode(byte[] payload)
        {
            if (payload == null || payload.Length > Capacity - Header)
                throw new InvalidDataException("Tamanho do snapshot inválido.");
            using (var input = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(input, Utf8))
            {
                var value = new Snapshot
                {
                    Session = new Guid(reader.ReadBytes(16)),
                    Character = reader.ReadInt32(),
                    Mode = reader.ReadByte(),
                    BlocksInput = reader.ReadBoolean(),
                    Incomplete = reader.ReadBoolean(),
                    Balance = ReadNumber(reader, true),
                    Title = ReadText(reader)
                };
                int count = reader.ReadInt32();
                if (value.Session == Guid.Empty || value.Character == 0 || value.Mode > 2 || count < 0 || count > MaxRows)
                    throw new InvalidDataException("Metadados inválidos.");
                for (int i = 0; i < count; i++)
                {
                    byte group = reader.ReadByte();
                    if (group > 5) throw new InvalidDataException("Grupo inválido.");
                    value.Rows.Add(new Entry { Group = group, Quantity = ReadNumber(reader, false), Price = ReadNumber(reader, false),
                        Id = ReadText(reader), Name = ReadText(reader), Category = ReadText(reader),
                        Description = ReadText(reader), Detail = ReadText(reader) });
                }
                if (input.Position != input.Length) throw new InvalidDataException("Dados excedentes.");
                return value;
            }
        }

        private static double ReadNumber(BinaryReader reader, bool signed)
        {
            double value = reader.ReadDouble();
            if (double.IsInfinity(value) || (!signed && value < 0))
                throw new InvalidDataException("Valor numérico inválido.");
            return value;
        }

        private static void WriteText(BinaryWriter writer, string text)
        {
            text = text ?? "";
            int use = Math.Min(text.Length, MaxTextBytes / 4);
            if (use > 0 && char.IsHighSurrogate(text[use - 1])) use--;
            byte[] bytes = Encoding.UTF8.GetBytes(text.Substring(0, use));
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadText(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > MaxTextBytes) throw new InvalidDataException("Campo de texto inválido.");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return Utf8.GetString(bytes);
        }
    }
}
