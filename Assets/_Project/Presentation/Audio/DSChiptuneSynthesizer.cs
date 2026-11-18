using System;
using UnityEngine;

namespace TBDA.Presentation.Audio
{
    /// <summary>
    /// Lightweight procedural Nintendo DS / GBA chiptune sound generator.
    /// Produces crisp square/triangle retro sound effects in memory without external audio bloat.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class DSChiptuneSynthesizer : MonoBehaviour
    {
        private AudioSource _source;
        private const int SampleRate = 22050;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.volume = 0.25f;
        }

        public void PlayUiTap() => PlayToneSequence(new[] { 660f, 880f }, 0.045f, squareWave: true);
        public void PlayTypewriterBlip() => PlayToneSequence(new[] { 520f }, 0.025f, squareWave: true);
        public void PlayPetLove() => PlayToneSequence(new[] { 523.25f, 659.25f, 783.99f }, 0.07f, squareWave: false);
        public void PlayEmailChime() => PlayToneSequence(new[] { 587.33f, 880f, 1174.66f }, 0.11f, squareWave: false);
        public void PlayLevelUp() => PlayToneSequence(new[] { 440f, 554.37f, 659.25f, 880f }, 0.085f, squareWave: true);
        public void PlayDungeonBootWarp() => PlayToneSequence(new[] { 220f, 330f, 440f, 660f, 880f }, 0.075f, squareWave: true);

        private void PlayToneSequence(float[] frequencies, float noteDurationSec, bool squareWave)
        {
            if (_source == null || frequencies == null || frequencies.Length == 0) return;

            int samplesPerNote = Mathf.Max(64, Mathf.RoundToInt(SampleRate * noteDurationSec));
            int totalSamples = samplesPerNote * frequencies.Length;
            float[] buffer = new float[totalSamples];

            for (int n = 0; n < frequencies.Length; n++)
            {
                float freq = frequencies[n];
                for (int i = 0; i < samplesPerNote; i++)
                {
                    float t = (float)i / SampleRate;
                    float envelope = 1f - ((float)i / samplesPerNote);
                    float phase = (t * freq) % 1f;
                    float raw = squareWave
                        ? (phase < 0.5f ? 0.6f : -0.6f)
                        : (Mathf.Abs(phase * 4f - 2f) - 1f) * 0.8f;
                    buffer[n * samplesPerNote + i] = raw * envelope * 0.5f;
                }
            }

            var clip = AudioClip.Create("DS_SFX", totalSamples, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            _source.PlayOneShot(clip);
        }
    }
}
