using System.Diagnostics;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Core.Tests.Infrastructure
{
    /// <summary>
    /// Проверки границ каталога и точек повторного анализа.
    /// </summary>
    public sealed class SafeFilePathTests
    {
        /// <summary>
        /// Junction выше корня не мешает файлу внутри обычного каталога.
        /// Ссылка внутри корня и корень-ссылка по-прежнему отклоняются.
        /// </summary>
        [Test]
        public void Resolve_ChecksReparsePointsInsideRootOnly()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "HwSync-SafePath-" + Guid.NewGuid().ToString("N"));
            string parentTarget = Path.Combine(workspace, "parent-target");
            string root = Path.Combine(workspace, "parent-link", "data");
            string insideTarget = Path.Combine(workspace, "inside-target");
            string linkedRootTarget = Path.Combine(workspace, "linked-root-target");
            Directory.CreateDirectory(parentTarget);
            Directory.CreateDirectory(Path.Combine(parentTarget, "data"));
            Directory.CreateDirectory(insideTarget);
            Directory.CreateDirectory(linkedRootTarget);
            CreateJunction(Path.Combine(workspace, "parent-link"), parentTarget);
            CreateJunction(Path.Combine(root, "link"), insideTarget);
            string linkedRoot = Path.Combine(workspace, "linked-root");
            CreateJunction(linkedRoot, linkedRootTarget);
            try
            {
                string resolved = SafeFilePath.Resolve(root, "file.txt");
                Assert.That(resolved, Is.EqualTo(Path.Combine(root, "file.txt")));
                Assert.Throws<IOException>(() => SafeFilePath.Resolve(root, "link/file.txt"));
                Assert.Throws<IOException>(() => SafeFilePath.Resolve(linkedRoot, "file.txt"));
            }
            finally
            {
                RemoveJunction(Path.Combine(root, "link"));
                RemoveJunction(Path.Combine(workspace, "parent-link"));
                RemoveJunction(linkedRoot);
                if (Directory.Exists(workspace))
                {
                    Directory.Delete(workspace, true);
                }
            }
        }

        /// <summary>
        /// Создаёт junction без прав администратора.
        /// </summary>
        private static void CreateJunction(string junction, string target)
        {
            ProcessStartInfo start = new("cmd.exe", $"/c mklink /J \"{junction}\" \"{target}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using Process process = Process.Start(start)!;
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Assert.Fail(error);
            }
        }

        /// <summary>
        /// Снимает junction, не удаляя каталог, на который он указывает.
        /// </summary>
        private static void RemoveJunction(string junction)
        {
            if (!Directory.Exists(junction))
            {
                return;
            }

            ProcessStartInfo start = new("cmd.exe", $"/c rmdir \"{junction}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using Process process = Process.Start(start)!;
            process.WaitForExit();
        }
    }
}
