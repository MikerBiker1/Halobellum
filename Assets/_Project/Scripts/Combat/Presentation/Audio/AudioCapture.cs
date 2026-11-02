// AudioCapture.cs
// Test tool: records what the game really outputs to a WAV file, and keeps the room silent while it does. It does nothing unless the
// game is started with "-audioCapture <file.wav>". It lets someone who can't hear check by measurement that the right things play at
// sensible levels. It sits on the scene's AudioListener and copies each block of the final mix. Next to the file it writes a cue sheet: what
// was asked for and when, in seconds of the recording.
// Depends on: UnityEngine.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class AudioCapture : MonoBehaviour
    {
        public const string Flag = "-audioCapture";
        private const int MaxSeconds = 600;

        private static string _path;
        private static bool _checked;
        private static float[] _buffer;
        private static int _written;
        private static int _channels = 2;
        private static int _rate = 48000;
        private static bool _saved;
        private static readonly List<string> CueSheet = new List<string>();

        /// <summary>True if this run records.</summary>
        public static bool IsRecording => _path != null;

        /// <summary>Writes a line in the cue sheet.</summary>
        public static void Note(string what)
        {
            if (_path != null) CueSheet.Add($"{_written / (float)(Math.Max(1, _channels) * _rate):0.00} {what}");
        }

        /// <summary>Puts the recorder on the listener in use (fallback, or else the one the scene has), if this run records at all.</summary>
        public static void AttachToActiveListener(AudioListener fallback)
        {
            if (!_checked)
            {
                _checked = true;
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, Flag);
                if (index >= 0 && index + 1 < args.Length) _path = args[index + 1];
            }
            if (_path == null) return;
            if (_buffer == null)
            {
                _rate = AudioSettings.outputSampleRate;
                _buffer = new float[_rate * 2 * MaxSeconds];
                Application.quitting += Save;
            }
            AudioListener listener = fallback;
            if (listener == null)
            {
                foreach (AudioListener candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                {
                    if (candidate.isActiveAndEnabled) listener = candidate;
                }
            }
            if (listener != null && listener.GetComponent<AudioCapture>() == null) listener.gameObject.AddComponent<AudioCapture>();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            _channels = channels;
            if (_buffer != null)
            {
                int count = Math.Min(data.Length, _buffer.Length - _written);
                if (count > 0)
                {
                    Array.Copy(data, 0, _buffer, _written, count);
                    _written += count;
                }
            }
            Array.Clear(data, 0, data.Length); // Silent in the room.
        }

        private static void Save()
        {
            if (_saved || _buffer == null) return;
            _saved = true;
            int frames = _written / Math.Max(1, _channels);
            using (var file = new BinaryWriter(File.Create(_path)))
            {
                int bytes = frames * 2 * 2;
                file.Write(new[] { 'R', 'I', 'F', 'F' });
                file.Write(36 + bytes);
                file.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                file.Write(16);
                file.Write((short)1);
                file.Write((short)2);
                file.Write(_rate);
                file.Write(_rate * 4);
                file.Write((short)4);
                file.Write((short)16);
                file.Write(new[] { 'd', 'a', 't', 'a' });
                file.Write(bytes);
                for (int i = 0; i < frames; i++)
                {
                    for (int c = 0; c < 2; c++)
                    {
                        float sample = _buffer[i * _channels + Math.Min(c, _channels - 1)];
                        file.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 32767f));
                    }
                }
            }
            File.WriteAllLines(_path + ".txt", CueSheet);
            Debug.Log($"Audio capture: {frames / (float)_rate:0.0} s saved to {_path}");
        }
    }
}
