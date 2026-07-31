namespace AirSticker.Runtime.Scripts.Core
{
    /// <summary>
    ///     Step 0 measurement switch for the Job System migration evaluation (see JobSystemMigrationPlan.md).
    ///     Set Enabled to true to log Stopwatch-based timings of the decal projection pipeline via Debug.Log.
    ///     It is disabled by default so normal usage of the package is not affected.
    /// </summary>
    public static class AirStickerPerformanceLog
    {
        public static bool Enabled;
    }
}
