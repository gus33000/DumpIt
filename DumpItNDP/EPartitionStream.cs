// Copyright (c) 2018, Gustave M. - gus33000.me - @gus33000
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DumpIt
{
    internal class EPartitionStream : Stream, IDisposable
    {
        private Stream? innerstream;
        private readonly string[] excluded;
        private readonly List<GPTPartition> partitions;
        private readonly bool IS_UNLOCKED = false;
        private readonly uint SectorSize;

        private bool disposed;

        public EPartitionStream(Stream stream, uint SectorSize, string[] partitionstoexclude)
        {
            this.SectorSize = SectorSize;
            innerstream = stream;
            excluded = partitionstoexclude;
            partitions = GetPartsFromGPT(this, SectorSize);

            if (partitions.Any(x => x.Name == "IS_UNLOCKED"))
            {
                IS_UNLOCKED = true;
            }
        }

        public override bool CanRead => innerstream.CanRead;
        public override bool CanSeek => innerstream.CanSeek;
        public override bool CanWrite => innerstream.CanWrite;
        public override long Length => innerstream.Length;
        public override long Position
        {
            get => innerstream.Position; set => innerstream.Position = value;
        }

        public override void Flush()
        {
            innerstream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ulong readingstart = Convert.ToUInt64(Position);
            ulong readingend = readingstart + Convert.ToUInt64(count);
            int read = innerstream.Read(buffer, offset, count);

            foreach (GPTPartition partition in partitions)
            {
                if (IS_UNLOCKED && partition.Name == "UEFI_BS_NV")
                {
                    continue;
                }

                // The partition is excluded.
                if (excluded.Any(x => x.Equals(partition.Name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    if (readingend < (partition.FirstSector * SectorSize))
                    {
                        continue;
                    }

                    if (readingstart > (partition.LastSector * SectorSize))
                    {
                        continue;
                    }

                    // We read inside the partition
                    if (readingstart >= (partition.FirstSector * SectorSize) && readingend <= (partition.LastSector * SectorSize))
                    {
                        for (int i = offset; i < count; i++)
                        {
                            buffer[i] = 0;
                        }

                        return read;
                    }

                    // We read beyond the partition in every way
                    if (readingstart < (partition.FirstSector * SectorSize) && readingend > (partition.LastSector * SectorSize))
                    {
                        for (int i = (int)(partition.FirstSector * SectorSize) - (int)readingstart + offset;
                            i < (int)(readingend - readingstart);
                            i++)
                        {
                            buffer[i] = 0;
                        }
                    }

                    // We read from inside the partition to beyond the partition.
                    if (readingstart >= (partition.FirstSector * SectorSize) && readingstart <= (partition.LastSector * SectorSize) && readingend > (partition.LastSector * SectorSize))
                    {
                        int bytecounttoremoveatthestart = (int)((partition.LastSector * SectorSize) - readingstart);
                        for (int i = offset; i < offset + bytecounttoremoveatthestart; i++)
                        {
                            buffer[i] = 0;
                        }
                    }

                    // We read from outside the partition to inside the partition and no partition before is excluded.
                    if (readingstart < (partition.FirstSector * SectorSize) && readingend <= (partition.LastSector * SectorSize) && readingend >= (partition.FirstSector * SectorSize))
                    {
                        int bytecounttoremoveattheend = (int)(readingend - (partition.FirstSector * SectorSize));
                        for (int i = count - bytecounttoremoveattheend; i < count; i++)
                        {
                            buffer[i] = 0;
                        }
                    }
                }
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return innerstream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            innerstream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            innerstream.Write(buffer, offset, count);
        }

        public static List<GPTPartition> GetPartsFromGPT(Stream stream, uint SectorSize)
        {
            byte[] GPTBuffer = new byte[0x100 * SectorSize];
            _ = stream.Read(GPTBuffer, 0, 0x100 * (int)SectorSize);

            GPT GPT = new(GPTBuffer, SectorSize);

            return GPT.Partitions;
        }

        public override void Close()
        {
            innerstream.Dispose();
            innerstream = null;
            base.Close();
        }

        public new void Dispose()
        {
            Dispose(true);
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        protected new void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!disposed)
            {
                if (disposing)
                {
                    if (innerstream != null)
                    {
                        innerstream.Dispose();
                        innerstream = null;
                    }
                }

                // Note disposing has been done.
                disposed = true;
            }
        }
    }
}
