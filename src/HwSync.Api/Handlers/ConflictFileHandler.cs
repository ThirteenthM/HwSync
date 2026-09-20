using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace HwSync.Api.Handlers
{
    /// <summary>
    /// Проверка и применение выбранной клиентом стратегии конфликта.
    /// </summary>
    public sealed class ConflictFileHandler
    {
        private readonly IScanJobService _jobs;
        private readonly IConflictFileOperations _conflicts;
        private readonly IComparedFileOperations _files;
        private readonly ISourceFileReader _reader;

        /// <summary>
        /// Принимает задания и проверяемые файловые операции.
        /// </summary>
        public ConflictFileHandler(IScanJobService jobs, IConflictFileOperations conflicts,
            IComparedFileOperations files, ISourceFileReader reader)
        {
            _jobs = jobs;
            _conflicts = conflicts;
            _files = files;
            _reader = reader;
        }

        /// <summary>
        /// Принимает клиентскую версию только для конфликта завершённого сравнения.
        /// </summary>
        public async Task<IActionResult> ExecuteAsync(Guid id, string relativePath, bool preserve, Stream body, CancellationToken token)
        {
            ScanJob? job = _jobs.Get(id);
            if (job is null)
            {
                return new NotFoundResult();
            }

            if (job.Status != ScanJobStatus.Completed || job.SourceRootPath is null)
            {
                return new ConflictResult();
            }

            FileChange? change = job.Changes?.SingleOrDefault(item => item.ChangeType == FileChangeType.Modified
                && item.Current?.RelativePath == relativePath && item.Previous?.RelativePath == relativePath);
            if (change?.Previous is not FileSnapshot client || change.Current is not FileSnapshot server)
            {
                return new ConflictObjectResult("Файл не является конфликтом выбранного сравнения.");
            }

            try
            {
                if (preserve)
                {
                    // Не создаём копию для уже устаревшего серверного снимка.
                    using Stream original = _reader.OpenRead(job.SourceRootPath, server);
                    FileSnapshot backup = new(ConflictCopyPath.Create(relativePath, id), client.Size, client.LastWriteTimeUtc);
                    await _files.CopyMissingAsync(job.SourceRootPath, backup, body, token);
                }
                else
                {
                    await _conflicts.ReplaceAsync(job.SourceRootPath, server, client,
                        (output, cancellation) => CopyVersionAsync(body, output, client.Size, cancellation), token);
                }

                return new NoContentResult();
            }
            catch (IOException exception)
            {
                return new ConflictObjectResult(new ProblemDetails { Status = 409, Detail = exception.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return new StatusCodeResult(403);
            }
            catch (ArgumentException)
            {
                return new BadRequestResult();
            }
        }
        /// <summary>
        /// Принимает ровно объём версии из снимка, ограничивая запись на диск.
        /// </summary>
        private static async Task CopyVersionAsync(Stream input, Stream output, long size, CancellationToken token)
        {
            byte[] buffer = new byte[81920];
            long remaining = size;
            while (remaining > 0)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), token);
                if (read == 0)
                {
                    throw new IOException("Передача новой версии прервана.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), token);
                remaining -= read;
            }

            if (await input.ReadAsync(buffer.AsMemory(0, 1), token) != 0)
            {
                throw new IOException("Размер новой версии превышает снимок.");
            }
        }
    }
}
