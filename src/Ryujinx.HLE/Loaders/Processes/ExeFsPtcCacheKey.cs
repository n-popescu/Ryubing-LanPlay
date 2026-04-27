using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

using Ryujinx.Common;

namespace Ryujinx.HLE.Loaders.Processes
{
    internal static class ExeFsPtcCacheKey
    {
        public readonly record struct SlotInfo(
            string Name,
            bool Present,
            bool Stubbed,
            byte[] Program,
            uint TextOffset,
            uint TextSize,
            uint RoOffset,
            uint RoSize,
            uint DataOffset,
            uint DataSize,
            uint BssSize);

        public static string ComputeSuffix(ReadOnlySpan<SlotInfo> slots, ReadOnlySpan<byte> npdmBytes)
        {
            using MemoryStream stream = new();

            WriteAscii(stream, "Ryujinx.ExeFsPtc.v1");
            WriteInt32(stream, slots.Length);

            foreach (SlotInfo slot in slots)
            {
                WriteString(stream, slot.Name);
                stream.WriteByte(slot.Present ? (byte)1 : (byte)0);
                stream.WriteByte(slot.Stubbed ? (byte)1 : (byte)0);
                WriteUInt32(stream, slot.TextOffset);
                WriteUInt32(stream, slot.TextSize);
                WriteUInt32(stream, slot.RoOffset);
                WriteUInt32(stream, slot.RoSize);
                WriteUInt32(stream, slot.DataOffset);
                WriteUInt32(stream, slot.DataSize);
                WriteUInt32(stream, slot.BssSize);
                WriteBytes(stream, slot.Program);
            }

            WriteBytes(stream, npdmBytes);

            Hash128 hash = Hash128.ComputeHash(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
            Span<byte> bytes = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes[..8], hash.Low);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], hash.High);

            return "-" + Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }

        private static void WriteAscii(MemoryStream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            WriteBytes(stream, bytes);
        }

        private static void WriteString(MemoryStream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteBytes(stream, bytes);
        }

        private static void WriteBytes(MemoryStream stream, ReadOnlySpan<byte> bytes)
        {
            WriteInt32(stream, bytes.Length);
            stream.Write(bytes);
        }

        private static void WriteInt32(MemoryStream stream, int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void WriteUInt32(MemoryStream stream, uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }
    }
}
