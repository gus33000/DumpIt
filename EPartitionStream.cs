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
using System.Text;

namespace DumpIt
{
    internal class EPartitionStream : Stream
    {
        private Stream innerstream;
        private readonly string[] excluded;
        private readonly GPTPartition[] partitions;
        private readonly bool IS_UNLOCKED = false;

        private bool disposed;

        public EPartitionStream(Stream stream, string[] partitionstoexclude)
        {
            innerstream = stream;
            excluded = partitionstoexclude;
            partitions = GetPartsFromGPT(this);

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
                if (excluded.Any(x => x.ToLower() == partition.Name.ToLower()))
                {
                    if (readingend < partition.FirstLBA)
                    {
                        continue;
                    }

                    if (readingstart > partition.LastLBA)
                    {
                        continue;
                    }

                    // We read inside the partition
                    if (readingstart >= partition.FirstLBA && readingend <= partition.LastLBA)
                    {
                        for (int i = offset; i < count; i++)
                        {
                            buffer[i] = 0;
                        }

                        return read;
                    }

                    // We read beyond the partition in every way
                    if (readingstart < partition.FirstLBA && readingend > partition.LastLBA)
                    {
                        for (int i = (int)partition.FirstLBA - (int)readingstart + offset;
                            i < (int)(readingend - readingstart);
                            i++)
                        {
                            buffer[i] = 0;
                        }
                    }

                    // We read from inside the partition to beyond the partition.
                    if (readingstart >= partition.FirstLBA && readingstart <= partition.LastLBA && readingend > partition.LastLBA)
                    {
                        int bytecounttoremoveatthestart = (int)(partition.LastLBA - readingstart);
                        for (int i = offset; i < offset + bytecounttoremoveatthestart; i++)
                        {
                            buffer[i] = 0;
                        }
                    }

                    // We read from outside the partition to inside the partition and no partition before is excluded.
                    if (readingstart < partition.FirstLBA && readingend <= partition.LastLBA && readingend >= partition.FirstLBA)
                    {
                        int bytecounttoremoveattheend = (int)(readingend - partition.FirstLBA);
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

        public static GPTPartition[] GetPartsFromGPT(Stream ds)
        {
            string GPTSignature = "EFI PART";
            byte[] partitionArray = null;
            _ = ds.Seek(0, SeekOrigin.Begin);
            byte[] sector = new byte[Constants.SectorSize]; // 512d, regular sector size
            int read = ds.Read(sector, 0, sector.Length);
            if (read == sector.Length && Encoding.ASCII.GetString(sector, 0, 8) != GPTSignature)
            {
                read = ds.Read(sector, 0, sector.Length);
            }

            if (read == sector.Length && Encoding.ASCII.GetString(sector, 0, 8) == GPTSignature)
            {
                uint partitionSlotCount = BitConverter.ToUInt32(sector, 0x50); // partition count from header
                uint partitionSlotSize = BitConverter.ToUInt32(sector, 0x54); // partition size from header
                int bytesToRead = (int)Math.Round(partitionSlotCount * partitionSlotSize / (double)sector.Length,
                                      MidpointRounding.AwayFromZero) * sector.Length;
                partitionArray = new byte[bytesToRead];
                _ = ds.Read(partitionArray, 0, partitionArray.Length);
            }

            _ = ds.Seek(0, SeekOrigin.Begin);

            if (partitionArray == null)
            {
                Console.WriteLine("Failed to read partition array");
                throw new Exception("Failed to read partition array");
            }

            List<GPTPartition> partitionarray = new();

            using (BinaryReader br = new(new MemoryStream(partitionArray)))
            {
                byte[] name = new byte[72]; // fixed name size
                int iterator = 0;
                while (true)
                {
                    Guid type = new(br.ReadBytes(16));
                    if (type == Guid.Empty)
                    {
                        break;
                    }

                    _ = br.BaseStream.Seek(16, SeekOrigin.Current);
                    ulong firstLBA = br.ReadUInt64();
                    ulong lastLBA = br.ReadUInt64();
                    _ = br.BaseStream.Seek(0x8, SeekOrigin.Current);
                    name = br.ReadBytes(name.Length);
                    iterator++;
                    partitionarray.Add(new GPTPartition
                    {
                        Name = Encoding.Unicode.GetString(name).TrimEnd('\0'),
                        FirstLBA = firstLBA * Constants.SectorSize,
                        LastLBA = (lastLBA + 0) * Constants.SectorSize
                    });
                }
            }

            return partitionarray.ToArray();
        }

        internal class GPTPartition
        {
            public string Name
            {
                get; set;
            }
            public ulong FirstLBA
            {
                get; set;
            }
            public ulong LastLBA
            {
                get; set;
            }
        }

        public override void Close()
        {
            innerstream.Dispose();
            innerstream = null;
            base.Close();
        }

        private new void Dispose()
        {
            Dispose(true);
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        private new void Dispose(bool disposing)
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
