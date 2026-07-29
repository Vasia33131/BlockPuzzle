using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Synthesises the game's sound effects at runtime. The project deliberately ships no
    /// binary assets, so the clips are rendered into memory instead of being imported —
    /// which also keeps them free to retune, since a sound is described by a few numbers
    /// here rather than by a file.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;

        /// <summary>Ramp applied to both ends of a clip so it never starts or stops with a pop.</summary>
        private const float EdgeFadeSeconds = 0.004f;

        /// <summary>
        /// Dry, short click for dropping a figure on the board: a body tone that falls in
        /// pitch under a fast decay, with a pinch of noise for the initial tap.
        /// </summary>
        public static AudioClip CreateClick()
        {
            const float duration = 0.055f;
            int length = Mathf.CeilToInt(duration * SampleRate);
            var samples = new float[length];
            var random = new System.Random(1337);

            for (int i = 0; i < length; i++)
            {
                float time = i / (float)SampleRate;
                float progress = time / duration;

                float frequency = Mathf.Lerp(960f, 420f, progress * progress);
                float body = Mathf.Sin(2f * Mathf.PI * frequency * time);
                float noise = (float)(random.NextDouble() * 2d - 1d) * Mathf.Exp(-progress * 26f) * 0.25f;
                float envelope = Mathf.Exp(-progress * 11f);

                samples[i] = (body * 0.75f + noise) * envelope * 0.7f;
            }

            return Build("SFX_Place", samples);
        }

        /// <summary>
        /// Bright rising sparkle for a cleared line. <paramref name="steps"/> notes of a major
        /// arpeggio are played in sequence, so clearing more lines at once is not just louder
        /// but visibly longer and higher.
        /// </summary>
        public static AudioClip CreateSparkle(int steps = 4)
        {
            steps = Mathf.Clamp(steps, 1, 6);

            const float noteDuration = 0.085f;
            const float tail = 0.16f;

            // Major triad over two octaves, in semitones above the root.
            int[] intervals = { 0, 4, 7, 12, 16, 19 };
            float root = 784f; // G5

            float duration = noteDuration * steps + tail;
            int length = Mathf.CeilToInt(duration * SampleRate);
            var samples = new float[length];

            for (int step = 0; step < steps; step++)
            {
                float frequency = root * Mathf.Pow(2f, intervals[step] / 12f);
                int start = Mathf.RoundToInt(step * noteDuration * SampleRate);
                int noteLength = Mathf.Min(length - start, Mathf.CeilToInt((noteDuration + tail) * SampleRate));

                for (int i = 0; i < noteLength; i++)
                {
                    float time = i / (float)SampleRate;
                    float envelope = Mathf.Exp(-time * 13f);

                    // A touch of the octave above keeps the tone glassy instead of flat.
                    float tone = Mathf.Sin(2f * Mathf.PI * frequency * time) * 0.8f
                                 + Mathf.Sin(4f * Mathf.PI * frequency * time) * 0.2f;

                    samples[start + i] += tone * envelope * 0.36f;
                }
            }

            return Build("SFX_LineClear", samples);
        }

        private static AudioClip Build(string name, float[] samples)
        {
            ApplyEdgeFade(samples);
            Normalize(samples, 0.9f);

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void ApplyEdgeFade(float[] samples)
        {
            int fade = Mathf.Min(Mathf.CeilToInt(EdgeFadeSeconds * SampleRate), samples.Length / 2);

            for (int i = 0; i < fade; i++)
            {
                float gain = i / (float)fade;
                samples[i] *= gain;
                samples[samples.Length - 1 - i] *= gain;
            }
        }

        /// <summary>Scales the clip so its loudest sample sits at <paramref name="peak"/>.</summary>
        private static void Normalize(float[] samples, float peak)
        {
            float loudest = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(samples[i]));
            }

            if (loudest <= Mathf.Epsilon)
            {
                return;
            }

            float gain = peak / loudest;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }
    }
}
