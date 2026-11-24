// Framing.cs
// How messages travel over the TCP stream: each one is a 4-byte big-endian length followed by that many bytes of UTF-8 JSON.
// TCP delivers a stream of bytes, not messages, so one read can hold half a message or several; FrameReader collects the bytes
// and hands out whole messages. A frame longer than MaxFrameBytes (1 MB, LanLimits) is refused at once, before its bytes are collected.
// Plain C# 9 without the engine.
// Depends on: NetMessage, LanTimings (LanLimits).

using System;
using System.Text;

namespace Halobellum.Net
{
    /// <summary>A frame that breaks the rules (too long, negative length).</summary>
    public sealed class FrameException : Exception
    {
        public FrameException(string message) : base(message) { }
    }

    /// <summary>Turns messages into frames.</summary>
    public static class Framing
    {
        public const int HeaderBytes = 4;
        /// <summary>The longest payload, in bytes (LanLimits.MaxFrameBytes, 1 MB).</summary>
        public const int MaxFrameBytes = LanLimits.MaxFrameBytes;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>The frame of a message: length header plus its UTF-8 JSON.</summary>
        public static byte[] Encode(JsonObject message) => Encode(NetJson.Write(message));

        /// <summary>The frame of a JSON text. Throws FrameException when the text is longer than MaxFrameBytes in UTF-8.</summary>
        public static byte[] Encode(string json)
        {
            byte[] payload = Utf8.GetBytes(json);
            if (payload.Length > MaxFrameBytes)
                throw new FrameException("A message of " + payload.Length + " bytes is over the limit of " + MaxFrameBytes + " bytes.");
            var frame = new byte[HeaderBytes + payload.Length];
            WriteLength(frame, payload.Length);
            Buffer.BlockCopy(payload, 0, frame, HeaderBytes, payload.Length);
            return frame;
        }

        /// <summary>Reads a frame's payload as text. Throws FrameException when it is not valid UTF-8.</summary>
        public static string DecodeText(byte[] payload)
        {
            try { return Utf8.GetString(payload); }
            catch (ArgumentException e) { throw new FrameException("A message is not valid UTF-8: " + e.Message); }
        }

        internal static void WriteLength(byte[] buffer, int length)
        {
            buffer[0] = (byte)(length >> 24);
            buffer[1] = (byte)(length >> 16);
            buffer[2] = (byte)(length >> 8);
            buffer[3] = (byte)length;
        }

        internal static int ReadLength(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];
        }
    }

    /// <summary>
    /// Collects bytes as they arrive and hands out whole frame payloads. Push what a read returned, then call TryRead until it
    /// says false. Not thread-safe: one reader thread owns it.
    /// </summary>
    public sealed class FrameReader
    {
        private readonly int maxFrameBytes;
        private byte[] buffer = new byte[4096];
        private int start;  // first unread byte
        private int end;    // one past the last byte held

        public FrameReader(int maxFrameBytes = Framing.MaxFrameBytes)
        {
            if (maxFrameBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxFrameBytes));
            this.maxFrameBytes = maxFrameBytes;
        }

        /// <summary>Bytes held that are not yet part of a whole frame handed out.</summary>
        public int Buffered => end - start;

        /// <summary>Adds bytes read from the stream. Throws FrameException as soon as a frame's header is over the limit.</summary>
        public void Push(byte[] data, int offset, int count)
        {
            if (count <= 0) return;
            if (buffer.Length - end < count)
            {
                int held = end - start;
                if (buffer.Length - held >= count && start > 0)
                {
                    Buffer.BlockCopy(buffer, start, buffer, 0, held); // move what is held to the front
                }
                else
                {
                    int size = buffer.Length;
                    while (size - held < count) size *= 2;
                    var bigger = new byte[size];
                    Buffer.BlockCopy(buffer, start, bigger, 0, held);
                    buffer = bigger;
                }
                start = 0;
                end = held;
            }
            Buffer.BlockCopy(data, offset, buffer, end, count);
            end += count;
            CheckHeader();
        }

        /// <summary>The next whole frame's payload, if one has fully arrived.</summary>
        public bool TryRead(out byte[] payload)
        {
            payload = null;
            if (end - start < Framing.HeaderBytes) return false;
            int length = CheckHeader();
            if (end - start < Framing.HeaderBytes + length) return false;
            payload = new byte[length];
            Buffer.BlockCopy(buffer, start + Framing.HeaderBytes, payload, 0, length);
            start += Framing.HeaderBytes + length;
            if (start == end) start = end = 0;
            else CheckHeader();
            return true;
        }

        /// <summary>The next whole frame as text, if one has fully arrived.</summary>
        public bool TryReadText(out string json)
        {
            json = null;
            if (!TryRead(out byte[] payload)) return false;
            json = Framing.DecodeText(payload);
            return true;
        }

        private int CheckHeader()
        {
            if (end - start < Framing.HeaderBytes) return 0;
            int length = Framing.ReadLength(buffer, start);
            if (length < 0 || length > maxFrameBytes)
                throw new FrameException("A message claims " + (uint)length + " bytes, over the limit of " + maxFrameBytes + " bytes.");
            return length;
        }
    }
}
