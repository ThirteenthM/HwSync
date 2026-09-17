namespace HwSync.Abstractions.Models
{
    /// <summary>Серверная отметка удаления с последними известными атрибутами файла.</summary>
    public sealed record DeletedFile(string RelativePath, bool Deleted, DateTimeOffset DeletedAtUtc, long ChangeNumber, FileSnapshot PreviousFile);
}
