namespace HwSync.Windows.Contract.Client.Configuration
{
    /// <summary>
    /// Общее правило обработки различий; допустимость зависит от настройки профиля.
    /// </summary>
    public enum SyncRule
    {
        Copy,
        Skip,
        Keep,
        RecordOnly,
        Delete,
        AskUser,
        KeepBoth
    }
}
