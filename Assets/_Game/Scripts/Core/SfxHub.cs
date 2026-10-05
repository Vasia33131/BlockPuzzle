using System;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// The single door to the sound effects. UI, pieces and managers only say <em>what</em> happened;
    /// the <c>AudioManager</c> listens here and decides how it sounds. Keeps the low layers free of
    /// a reference to the audio code.
    /// </summary>
    public static class SfxHub
    {
        /// <summary>Raised with the event, a pitch multiplier and a start delay in seconds.</summary>
        public static event Action<SfxId, float, float> Requested;

        public static void Play(SfxId id, float pitch = 1f, float delay = 0f) => Requested?.Invoke(id, pitch, delay);

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Requested = null;
    }
}
