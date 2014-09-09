using System;
using System.IO;
using JetBrains.Annotations;

namespace Testosterone {
    // Class used to count bytes read-from/written-to non-seekable streams.
    internal class LoggingStream : Stream {
        public event EventHandler<DataTransferEventArgs> DataRead;
        public event EventHandler<DataTransferEventArgs> DataWritten;
        public event EventHandler<DataRequestedEventArgs> ReadRequested;
        public event EventHandler<DataTransferEventArgs> WriteRequested;
        readonly Stream baseStream;

        // These are necessary to avoid counting bytes twice if ReadByte/WriteByte call Read/Write internally.
        bool readingOneByte, writingOneByte;

        // These are necessary to avoid counting bytes twice if Read/Write call ReadByte/WriteByte internally.
        bool readingManyBytes, writingManyBytes;


        public LoggingStream([NotNull] Stream stream) {
            if (stream == null) throw new ArgumentNullException("stream");
            baseStream = stream;
        }


        public override void Flush() {
            baseStream.Flush();
        }


        public override long Seek(long offset, SeekOrigin origin) {
            return baseStream.Seek(offset, origin);
        }


        public override void SetLength(long value) {
            baseStream.SetLength(value);
        }


        public override int Read(byte[] buffer, int offset, int count) {
            readingManyBytes = true;
            int bytesActuallyRead;

            if (readingOneByte) {
                // This is WriteByte backed by Read: don't raise events
                bytesActuallyRead = baseStream.Read(buffer, offset, count);
            } else {
                // Not a single-read op. Raise ReadRequested/DataRead events!
                ReadRequested.Raise(this, new DataRequestedEventArgs(count));

                bytesActuallyRead = baseStream.Read(buffer, offset, count);

                BytesRead += bytesActuallyRead;
                byte[] readData = new byte[bytesActuallyRead];
                Buffer.BlockCopy(buffer, offset, readData, 0, bytesActuallyRead);
                DataRead.Raise(this, new DataTransferEventArgs(readData));
            }
            readingManyBytes = false;
            return bytesActuallyRead;
        }


        public override void Write(byte[] buffer, int offset, int count) {
            writingManyBytes = true;

            if (writingOneByte) {
                // This is WriteByte backed by Write: don't raise events
                baseStream.Write(buffer, offset, count);
            } else {
                // Not a single-write op. Raise WriteRequested/DataWritten events!
                byte[] dataToWrite = new byte[count];
                Buffer.BlockCopy(buffer, offset, dataToWrite, 0, count);
                WriteRequested.Raise(this, new DataTransferEventArgs(dataToWrite));

                baseStream.Write(buffer, offset, count);

                BytesWritten += count;
                DataWritten.Raise(this, new DataTransferEventArgs(dataToWrite));
            }

            writingManyBytes = false;
        }


        public override int ReadByte() {
            // Raise ReadRequested event, unless this is part of a multi-byte read op
            if (!readingManyBytes) {
                ReadRequested.Raise(this, new DataRequestedEventArgs(1));
            }

            readingOneByte = true;
            int value = base.ReadByte();
            readingOneByte = false;

            // Raise DataRead event, unless this is part of a multi-byte read op
            if (!readingManyBytes) {
                byte[] readData;
                if (value >= 0) {
                    readData = new[] { (byte)value };
                    BytesRead++;
                } else {
                    // a 0-byte read op indicates end-of-stream
                    readData = new byte[0];
                }
                DataRead.Raise(this, new DataTransferEventArgs(readData));
            }

            return value;
        }


        public override void WriteByte(byte value) {
            if (!writingManyBytes) {
                WriteRequested.Raise(this, new DataTransferEventArgs(new[] { value }));
            }

            writingOneByte = true;
            base.WriteByte(value);
            writingOneByte = false;

            if (!writingManyBytes) {
                BytesWritten++;
                DataWritten.Raise(this, new DataTransferEventArgs(new[] { value }));
            }
        }


        public override bool CanRead {
            get { return baseStream.CanRead; }
        }

        public override bool CanSeek {
            get { return baseStream.CanSeek; }
        }

        public override bool CanWrite {
            get { return baseStream.CanWrite; }
        }

        public override long Length {
            get { return baseStream.Length; }
        }

        public override long Position {
            get { return baseStream.Position; }
            set { baseStream.Position = value; }
        }

        public long BytesRead { get; private set; }
        public long BytesWritten { get; private set; }
    }


    internal class DataTransferEventArgs : EventArgs {
        public DataTransferEventArgs(byte[] data) {
            Data = data;
        }

        public byte[] Data { get; private set; }
    }


    internal class DataRequestedEventArgs : EventArgs {
        public DataRequestedEventArgs(int bytesRequested) {
            BytesRequested = bytesRequested;
        }

        public int BytesRequested { get; private set; }
    }
}
