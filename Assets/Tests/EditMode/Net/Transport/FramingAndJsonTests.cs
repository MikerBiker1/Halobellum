// FramingAndJsonTests.cs
// Tests the LAN messages' bytes: frames split across reads, several frames in one read, a frame over the 4 MB limit refused,
// and the JSON of a message read back exactly as written (escapes, unicode, exact floats and doubles, 64-bit unsigned hashes,
// nested objects, arrays).
// Depends on: Halobellum.Net (Framing, FrameReader, NetJson, NetMessage), NUnit.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Halobellum.Net;
using NUnit.Framework;

namespace Halobellum.Tests.Net
{
    public class FramingAndJsonTests
    {
        private static byte[] Join(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        private static List<string> ReadAll(FrameReader reader)
        {
            var result = new List<string>();
            while (reader.TryReadText(out string json)) result.Add(json);
            return result;
        }

        [Test]
        public void Frame_SplitAcrossReads_ComesOutWhole()
        {
            byte[] frame = Framing.Encode(new NetMessage("Ready").Set("round", 3).Set("note", "hello there"));
            var reader = new FrameReader();
            // One byte at a time: nothing comes out until the last byte is in.
            for (int i = 0; i < frame.Length - 1; i++)
            {
                reader.Push(frame, i, 1);
                Assert.IsFalse(reader.TryRead(out _), "A frame came out after " + (i + 1) + " bytes.");
            }
            reader.Push(frame, frame.Length - 1, 1);
            List<string> got = ReadAll(reader);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("Ready", NetMessage.Parse(got[0]).Type);
            Assert.AreEqual(0, reader.Buffered);
        }

        [Test]
        public void SeveralFrames_InOneRead_AllComeOut_InOrder()
        {
            byte[] a = Framing.Encode(new NetMessage("A"));
            byte[] b = Framing.Encode(new NetMessage("B").Set("x", 1));
            byte[] c = Framing.Encode(new NetMessage("C").Set("s", new string('z', 10000)));
            byte[] all = Join(a, b, c);

            var reader = new FrameReader();
            reader.Push(all, 0, all.Length);
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, ReadAll(reader).Select(j => NetMessage.Parse(j).Type).ToArray());

            // The same bytes cut at awkward places (inside a header, inside a payload, across two frames).
            var random = new Random(5);
            for (int trial = 0; trial < 50; trial++)
            {
                var r = new FrameReader();
                var types = new List<string>();
                int pos = 0;
                while (pos < all.Length)
                {
                    int n = Math.Min(all.Length - pos, 1 + random.Next(trial % 2 == 0 ? 7 : 3000));
                    r.Push(all, pos, n);
                    pos += n;
                    types.AddRange(ReadAll(r).Select(j => NetMessage.Parse(j).Type));
                }
                CollectionAssert.AreEqual(new[] { "A", "B", "C" }, types, "trial " + trial);
            }
        }

        [Test]
        public void OversizeFrame_IsRefused_AsSoonAsItsHeaderArrives()
        {
            int tooLong = Framing.MaxFrameBytes + 1;
            var header = new[] { (byte)(tooLong >> 24), (byte)(tooLong >> 16), (byte)(tooLong >> 8), (byte)tooLong };
            var reader = new FrameReader();
            reader.Push(header, 0, 2); // half a header: nothing to judge yet
            Assert.Throws<FrameException>(() => reader.Push(header, 2, 2));

            // A negative length (top bit set) is refused too.
            var negative = new byte[] { 0x80, 0, 0, 1 };
            Assert.Throws<FrameException>(() => new FrameReader().Push(negative, 0, 4));

            // A smaller limit: a frame exactly at it passes, one byte more is refused.
            var small = new FrameReader(10);
            byte[] ten = Framing.Encode("{\"t\":\"ab\"}");
            Assert.AreEqual(14, ten.Length);
            small.Push(ten, 0, ten.Length);
            Assert.IsTrue(small.TryRead(out byte[] payload));
            Assert.AreEqual(10, payload.Length);
            byte[] eleven = Framing.Encode("{\"t\":\"abc\"}");
            Assert.Throws<FrameException>(() => small.Push(eleven, 0, eleven.Length));

            // Encoding a message over 4 MB is refused before it is sent.
            var huge = new NetMessage("Huge").Set("s", new string('x', Framing.MaxFrameBytes));
            Assert.Throws<FrameException>(() => Framing.Encode(huge));
        }

        [Test]
        public void Header_IsBigEndianLength()
        {
            byte[] frame = Framing.Encode("{\"t\":\"x\"}");
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 9 }, frame.Take(4).ToArray());
            Assert.AreEqual("{\"t\":\"x\"}", Encoding.UTF8.GetString(frame, 4, 9));
        }

        [Test]
        public void Json_RoundTrip_KeepsEverythingExactly()
        {
            string tricky = "quote\" back\\slash /slash\n\r\t\b\f \u0001\u001f é ß 日本語 😀 end";
            string lonely = "half \uD800 pair";
            float[] floats = { 0.1f, 1f / 3f, -123.456f, float.Epsilon, float.MaxValue, 16777217f, 0f, -0.75f };
            double[] doubles = { Math.PI, 0.1, 1e-300, -2.5e17, double.MaxValue };

            var m = new NetMessage("Test")
                .Set("tricky", tricky)
                .Set("lonely", lonely)
                .Set("empty", "")
                .Set("int", int.MinValue)
                .Set("long", long.MaxValue)
                .Set("yes", true)
                .Set("no", false)
                .SetNull("nothing")
                .Set("names", new[] { "a", "b\"c", "" })
                .Set("squads", new[] { new JsonObject().Set("id", 1).Set("x", 2.5f), new JsonObject().Set("id", 2).Set("inner", new JsonObject().Set("deep", "yes")) })
                .Set("none", new string[0])
                .Set("obj", new JsonObject());
            for (int i = 0; i < floats.Length; i++) m.Set("f" + i, floats[i]);
            for (int i = 0; i < doubles.Length; i++) m.Set("d" + i, doubles[i]);

            string json = m.ToJson();
            // Through the bytes of a frame as well.
            var reader = new FrameReader();
            byte[] frame = Framing.Encode(m);
            reader.Push(frame, 0, frame.Length);
            Assert.IsTrue(reader.TryReadText(out string fromFrame));
            Assert.AreEqual(json, fromFrame);

            NetMessage back = NetMessage.Parse(fromFrame);
            Assert.AreEqual("Test", back.Type);
            Assert.AreEqual(tricky, back.GetString("tricky"));
            Assert.AreEqual(lonely, back.GetString("lonely"));
            Assert.AreEqual("", back.GetString("empty"));
            Assert.AreEqual(int.MinValue, back.GetInt("int"));
            Assert.AreEqual(long.MaxValue, back.GetLong("long"));
            Assert.IsTrue(back.GetBool("yes"));
            Assert.IsFalse(back.GetBool("no", true));
            Assert.IsTrue(back.Has("nothing"));
            Assert.IsNull(back.Get("nothing"));
            CollectionAssert.AreEqual(new[] { "a", "b\"c", "" }, back.GetStringList("names"));
            List<JsonObject> squads = back.GetObjectList("squads");
            Assert.AreEqual(2, squads.Count);
            Assert.AreEqual(2.5f, squads[0].GetFloat("x"));
            Assert.AreEqual("yes", squads[1].GetObject("inner").GetString("deep"));
            Assert.AreEqual(0, back.GetStringList("none").Count);
            Assert.AreEqual(0, back.GetObject("obj").Count);
            for (int i = 0; i < floats.Length; i++)
                Assert.AreEqual(BitConverter.ToInt32(BitConverter.GetBytes(floats[i]), 0), BitConverter.ToInt32(BitConverter.GetBytes(back.GetFloat("f" + i)), 0), "float " + floats[i]);
            for (int i = 0; i < doubles.Length; i++)
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(doubles[i]), BitConverter.DoubleToInt64Bits(back.GetDouble("d" + i)), "double " + doubles[i]);

            // Written again, the text is the same (key order kept).
            Assert.AreEqual(json, back.ToJson());
        }

        [Test]
        public void Json_KeepsA64BitUnsignedHash_Exactly()
        {
            // A state hash or content fingerprint is a ulong. It must go out as its exact whole number, not as a float.
            ulong hash = 0xFEDCBA9876543210UL;
            var m = new NetMessage("StateHash").Set("round", 3).Set("hash", hash).Set("max", ulong.MaxValue).Set("zero", 0UL);
            string json = m.ToJson();
            StringAssert.Contains("\"hash\":18364758544493064720", json);
            NetMessage back = NetMessage.Parse(json);
            Assert.AreEqual(hash, back.GetULong("hash"));
            Assert.AreEqual(ulong.MaxValue, back.GetULong("max"));
            Assert.AreEqual(0UL, back.GetULong("zero", 7));
            Assert.AreEqual(7UL, back.GetULong("missing", 7));
            Assert.AreEqual(7UL, NetMessage.Parse("{\"t\":\"x\",\"n\":-1}").GetULong("n", 7), "a negative number is not a ulong");
            Assert.AreEqual(7UL, NetMessage.Parse("{\"t\":\"x\",\"n\":1.5}").GetULong("n", 7), "a fraction is not a ulong");
        }

        [Test]
        public void Json_ReadsStandardEscapes_AndWhitespace()
        {
            JsonObject o = NetJson.ParseObject(" {\n \"t\" : \"x\" ,\t\"s\":\"\\u00e9\\/\\uD83D\\uDE00\", \"n\" : -1.5e+2 , \"a\" : [ ] } ");
            Assert.AreEqual("é/😀", o.GetString("s"));
            Assert.AreEqual(-150.0, o.GetDouble("n"));
            Assert.AreEqual(-150, o.GetInt("n", -150)); // not a whole number: the fallback
            Assert.AreEqual(0, o.GetArray("a").Count);
        }

        [Test]
        public void Json_RefusesBrokenText()
        {
            string[] broken =
            {
                "", "[1]", "{", "{\"t\":1,}", "{\"t\" 1}", "{\"t\":\"x\"} extra", "{t:1}", "{\"t\":01}", "{\"t\":1.}", "{\"t\":-}",
                "{\"t\":\"a\nb\"}", "{\"t\":\"\\x\"}", "{\"t\":\"\\u12\"}", "{\"t\":tru}", "{\"t\":\"open}",
                "{\"a\":" + new string('[', 40) + new string(']', 40) + "}",
            };
            foreach (string text in broken)
                Assert.Throws<FormatException>(() => NetJson.ParseObject(text), "accepted: " + text);

            Assert.Throws<FormatException>(() => NetMessage.Parse("{\"x\":1}"), "a message without a type");
            Assert.Throws<FormatException>(() => NetMessage.Parse("{\"t\":5}"), "a message whose type is not a string");
            Assert.Throws<ArgumentException>(() => new JsonObject().Set("f", float.NaN));
            Assert.Throws<ArgumentException>(() => new JsonObject().SetRaw("x", 5)); // a plain int is not a JSON value kind
        }
    }
}
