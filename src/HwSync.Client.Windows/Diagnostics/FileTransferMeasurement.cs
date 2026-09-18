namespace HwSync.Client.Windows.Diagnostics
{
    /// <summary>
    /// Время и подтверждённый объём одной попытки копирования.
    /// </summary>
    public sealed record FileTransferMeasurement(string RelativePath, long ConfirmedBytes, TimeSpan Duration, string Outcome)
    {
        public double MebibytesPerSecond => Duration.TotalSeconds > 0
            ? ConfirmedBytes / 1048576d / Duration.TotalSeconds : 0;
        public string Volume => $"{ConfirmedBytes / 1048576d:F2} МиБ";
        public string Elapsed => $"{Duration.TotalSeconds:F3} с";
        public string Speed => ConfirmedBytes > 0 ? $"{MebibytesPerSecond:F2} МиБ/с" : "—";
    }
}
