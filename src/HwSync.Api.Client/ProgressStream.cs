namespace HwSync.Api.Client
{
    /// <summary>
    /// Поток, сообщающий о прогрессе без владения исходным потоком.
    /// </summary>
    internal sealed class ProgressStream : Stream
    {
        private readonly Stream _inner;
        private readonly Action _progress;

        /// <summary>
        /// Связывает поток с уведомлением об обработанном блоке.
        /// </summary>
        public ProgressStream(Stream inner, Action progress)
        {
            _inner = inner;
            _progress = progress;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        /// <summary>
        /// Сбрасывает буфер исходного потока.
        /// </summary>
        public override void Flush() => _inner.Flush();

        /// <summary>
        /// Асинхронно сбрасывает буфер исходного потока.
        /// </summary>
        public override Task FlushAsync(CancellationToken token) => _inner.FlushAsync(token);

        /// <summary>
        /// Перемещает позицию в исходном потоке.
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        /// <summary>
        /// Меняет длину исходного потока.
        /// </summary>
        public override void SetLength(long value) => _inner.SetLength(value);

        /// <summary>
        /// Читает блок и сообщает о прогрессе.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            if (read > 0)
            {
                _progress();
            }
            return read;
        }

        /// <summary>
        /// Асинхронно читает блок и сообщает о прогрессе.
        /// </summary>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                _progress();
            }
            return read;
        }

        /// <summary>
        /// Асинхронно читает блок и сообщает о прогрессе.
        /// </summary>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <summary>
        /// Записывает блок и сообщает о прогрессе.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            if (count > 0)
            {
                _progress();
            }
        }

        /// <summary>
        /// Асинхронно записывает блок и сообщает о прогрессе.
        /// </summary>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (buffer.Length > 0)
            {
                _progress();
            }
        }

        /// <summary>
        /// Асинхронно записывает блок и сообщает о прогрессе.
        /// </summary>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
