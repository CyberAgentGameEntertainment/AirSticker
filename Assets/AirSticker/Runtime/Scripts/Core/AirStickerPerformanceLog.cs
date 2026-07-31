namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     Diagnostic switch for profiling the decal projection pipeline.
    ///     <para>
    ///         Disabled by default; normal usage of the package is unaffected. Set <see cref="Enabled" /> to true
    ///         to log Stopwatch-based timings of each pipeline stage (clip stage / build stage / mesh upload) via
    ///         <c>Debug.Log</c> with the <c>[AirSticker][Perf]</c> prefix.
    ///     </para>
    ///     <para>
    ///         Intended for profiling and performance regression checks only, not for production use. While it is
    ///         enabled the projection jobs are completed synchronously on the main thread — instead of being polled
    ///         across frames without blocking — so that the actual job compute time can be measured. This blocks the
    ///         main thread for the duration of each launch, so leave it disabled outside of measurement runs.
    ///     </para>
    /// </summary>
    public static class AirStickerPerformanceLog
    {
        public static bool Enabled;
    }
}
