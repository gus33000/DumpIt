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

using CommandLine;
using DiscUtils.Containers;
using DiscUtils.Streams;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DumpIt
{
    internal class Program
    {
        internal static string[] partitions = Constants.partitions;

        private static void PrintLogo()
        {
            Logging.Log($"DumpIt {Assembly.GetExecutingAssembly().GetName().Version} - A tool to dump NT based devices");
            Logging.Log("Copyright (c) Gustave Monce and Contributors");
            Logging.Log("https://github.com/gus33000/DumpIt");
            Logging.Log("");
            Logging.Log("This program comes with ABSOLUTELY NO WARRANTY.");
            Logging.Log("This is free software, and you are welcome to redistribute it under certain conditions.");
            Logging.Log("");
        }

        private static int WrapAction(Action a)
        {
            try
            {
                a();
            }
            catch (Exception ex)
            {
                Logging.Log("Something happened.", Logging.LoggingLevel.Error);
                while (ex != null)
                {
                    Logging.Log(ex.Message, Logging.LoggingLevel.Error);
                    Logging.Log(ex.StackTrace, Logging.LoggingLevel.Error);
                    ex = ex.InnerException;
                }
                if (Debugger.IsAttached)
                {
                    _ = Console.ReadLine();
                }

                return 1;
            }

            return 0;
        }

        private static void ParseDumpOptions(Options o)
        {
            if (!string.IsNullOrEmpty(o.ExcludedFile) && File.Exists(o.ExcludedFile))
            {
                partitions = new List<string>(File.ReadAllLines(o.ExcludedFile)).ToArray();
            }

            ConvertDD2VHD(o.ImgFile, o.VhdFile, partitions, o.Recovery);
        }

        private static int Main(string[] args)
        {
            Assembly ass = Assembly.GetExecutingAssembly();

            return Parser.Default.ParseArguments<Options>(args).MapResult((Options opts) =>
            {
                PrintLogo();
                return WrapAction(() => ParseDumpOptions(opts));
            }, errs => 1);
        }

        public static Stream GetStreamFromFilePath(string ddfile, out ulong SectorSize)
        {
            Stream strm;
            SectorSize = Constants.SectorSize;

            if (ddfile.ToLower().Contains(@"\\.\PhysicalDrive"))
            {
                DeviceStream dstrm = new(ddfile, FileAccess.Read);

                SectorSize = dstrm.SectorSize;
                strm = dstrm;
            }
            else if (ddfile.ToLower().EndsWith(".vhd", StringComparison.InvariantCultureIgnoreCase))
            {
                DiscUtils.Vhd.Disk disk = new(ddfile, FileAccess.Read);

                SectorSize = (ulong)disk.SectorSize;
                strm = disk.Content;
            }
            else if (ddfile.ToLower().EndsWith(".vhdx", StringComparison.InvariantCultureIgnoreCase))
            {
                DiscUtils.Vhdx.Disk disk = new(ddfile, FileAccess.Read);

                SectorSize = (ulong)disk.SectorSize;
                strm = disk.Content;
            }
            else
            {
                strm = new FileStream(ddfile, FileMode.Open);
            }

            return strm;
        }

        /// <summary>
        ///     Coverts a raw DD image into a VHD file suitable for FFU imaging.
        /// </summary>
        /// <param name="ddfile">The path to the DD file.</param>
        /// <param name="vhdfile">The path to the output VHD file.</param>
        /// <returns></returns>
        public static void ConvertDD2VHD(string ddfile, string vhdfile, string[] partitions, bool Recovery)
        {
            SetupHelper.SetupContainers();

            Stream strm = GetStreamFromFilePath(ddfile, out ulong SectorSize);
            Stream fstream = !Recovery ? new EPartitionStream(strm, partitions) : strm;

            /*using DiscUtils.Raw.Disk inDisk = new(fstream, Ownership.Dispose);

            VirtualDiskParameters diskParams = inDisk.Parameters;
            using VirtualDisk outDisk = VirtualDisk.CreateDisk("VHD", "dynamic", vhdfile, diskParams, "", "");
            SparseStream contentStream = inDisk.Content;

            StreamPump pump = new()
            {
                InputStream = contentStream,
                OutputStream = outDisk.Content,
                SparseCopy = true,
                SparseChunkSize = (int)SectorSize,
                BufferSize = (int)SectorSize * 1024
            };

            long totalBytes = contentStream.Length;

            DateTime now = DateTime.Now;
            pump.ProgressEvent += (o, e) => { ShowProgress((ulong)e.BytesRead, (ulong)totalBytes, now); };

            Logging.Log("Converting RAW to VHD");
            pump.Run();
            Console.WriteLine();*/

            EPartitionStream.GPTPartition[] parts = EPartitionStream.GetPartsFromGPT(fstream);
            foreach (EPartitionStream.GPTPartition part in parts)
            {
                Stream contentStream = new PartialStream(fstream, (long)part.FirstLBA, (long)part.LastLBA + (long)SectorSize);

                using FileStream dst = File.Create(part.Name + ".img");

                StreamPump pump = new()
                {
                    InputStream = contentStream,
                    OutputStream = dst,
                    SparseCopy = true,
                    SparseChunkSize = (int)SectorSize,
                    BufferSize = (int)SectorSize * 1024
                };

                long totalBytes = contentStream.Length;

                DateTime now = DateTime.Now;
                pump.ProgressEvent += (o, e) => { ShowProgress((ulong)e.BytesRead, (ulong)totalBytes, now); };

                Logging.Log($"Dumping {part.Name} - {part.FirstLBA} - {part.LastLBA}");
                pump.Run();
                Logging.Log("");
            }
        }

        protected static void ShowProgress(ulong readBytes, ulong totalBytes, DateTime startTime)
        {
            DateTime now = DateTime.Now;
            TimeSpan timeSoFar = now - startTime;

            TimeSpan remaining =
                TimeSpan.FromMilliseconds(timeSoFar.TotalMilliseconds / readBytes * (totalBytes - readBytes));

            double speed = Math.Round(readBytes / 1024L / 1024L / timeSoFar.TotalSeconds);

            Logging.Log(
                $"{GetDismLikeProgBar((int)(readBytes * 100 / totalBytes))} {speed}MB/s {remaining:hh\\:mm\\:ss\\.f}",
                returnline: false);
        }

        private static string GetDismLikeProgBar(int perc)
        {
            int eqsLength = (int)((double)perc / 100 * 55);
            string bases = new string('=', eqsLength) + new string(' ', 55 - eqsLength);
            bases = bases.Insert(28, perc + "%");
            if (perc == 100)
            {
                bases = bases[1..];
            }
            else if (perc < 10)
            {
                bases = bases.Insert(28, " ");
            }

            return "[" + bases + "]";
        }
    }
}
