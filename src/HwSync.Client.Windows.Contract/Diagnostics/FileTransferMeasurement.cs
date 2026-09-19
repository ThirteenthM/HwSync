namespace HwSync.Client.Windows.Contract.Diagnostics
{
    /// <summary>
    /// Время и подтверждённый объём одной попытки копирования.
    /// </summary>
    public sealed record FileTransferMeasurement(string RelativePath, long ConfirmedBytes, TimeSpan Duration, string Outcome)
    {
        public double MegabytesPerSecond => Duration.TotalSeconds > 0 ? ConfirmedBytes / 1_000_000d / Duration.TotalSeconds : 0;

        public string Volume => $"{ConfirmedBytes / 1_000_000d:F2} МБ";

        public string Elapsed => $"{Duration.TotalSeconds:F3} с";

        public string Speed => ConfirmedBytes > 0 ? $"{MegabytesPerSecond:F2} МБ/с" : "—";
    }
}
