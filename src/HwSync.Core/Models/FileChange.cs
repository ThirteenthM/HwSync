using System;
using System.Collections.Generic;
using System.Text;

namespace HwSync.Core.Models
{
    public sealed record FileChange(
        FileChangeType ChangeType,
        FileSnapshot? Previous,
        FileSnapshot? Current
    );
}
