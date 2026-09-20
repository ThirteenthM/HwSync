using HwSync.Abstractions.Models;

namespace HwSync.Windows.Contract.Client.ViewModels
{
    /// <summary>
    /// Активная отметка удаления с необязательной последней известной версией.
    /// </summary>
    public sealed record FileDeletionEvidence(string RelativePath, FileSnapshot? PreviousFile);
}
