using System;
using System.IO;
using Game.Audio;

namespace ArtPreview
{
    /// <summary>Writes every procedural sound and loop as a 16-bit mono WAV, for listening outside Unity.</summary>
    static class AudioExport
    {
        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (Sfx s in Enum.GetValues(typeof(Sfx)))
                if (s != Sfx.None) Write(Path.Combine(dir, "sfx_" + s + ".wav"), Synth.Build(s), 1);
            foreach (Loop l in Enum.GetValues(typeof(Loop)))
                Write(Path.Combine(dir, "loop_" + l + ".wav"), Synth.BuildLoop(l), l == Loop.Music ? 2 : 3); // repeat to hear the seam
            Console.WriteLine("audio written to " + dir);
        }

        static void Write(string path, float[] pcm, int repeats)
        {
            int n = pcm.Length * repeats;
            using (var w = new BinaryWriter(File.Create(path)))
            {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Synth.SampleRate); w.Write(Synth.SampleRate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            for (int r = 0; r < repeats; r++)
                foreach (float v in pcm) w.Write((short)Math.Round(Math.Clamp(v, -1f, 1f) * 32767f));
            }
        }
    }
}
