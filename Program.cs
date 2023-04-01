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
using DiscUtils.Vhdx;
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
            if (!string.IsNullOrEmpty(o.Excludelist) && File.Exists(o.Excludelist))
            {
                partitions = new List<string>(File.ReadAllLines(o.Excludelist)).ToArray();
            }

            ulong eMMCDumpSize;
            ulong SectorSize = Constants.SectorSize;

            if (o.ImgFile.ToLower().Contains(@"\\.\physicaldrive"))
            {
                Logging.Log("Tool is running in Device Dump mode.");
                Logging.Log("Gathering disk geometry...");
                eMMCDumpSize = (ulong)GetDiskSize.GetDiskLength(@"\\.\PhysicalDrive" + o.ImgFile.ToLower().Replace(@"\\.\physicaldrive", ""));
                SectorSize = (ulong)GetDiskSize.GetDiskSectorSize(@"\\.\PhysicalDrive" + o.ImgFile.ToLower().Replace(@"\\.\physicaldrive", ""));
            }
            else
            {
                Logging.Log("Tool is running in Image Dump mode.");
                Logging.Log("Gathering disk image geometry...");
                eMMCDumpSize = (ulong)new FileInfo(o.ImgFile).Length;
            }

            Logging.Log("Reported source device eMMC size is: " + eMMCDumpSize + " bytes - " + (eMMCDumpSize / 1024 / 1024) + "MB - " + (eMMCDumpSize / 1024 / 1024 / 1024) + "GB.");
            Logging.Log("Selected " + SectorSize + "B for the sector size");

            ConvertDD2VHD(o.ImgFile, o.VhdFile, partitions, o.Recovery, SectorSize);
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

        /// <summary>
        ///     Coverts a raw DD image into a VHD file suitable for FFU imaging.
        /// </summary>
        /// <param name="ddfile">The path to the DD file.</param>
        /// <param name="vhdfile">The path to the output VHD file.</param>
        /// <returns></returns>
        public static void ConvertDD2VHD(string ddfile, string vhdfile, string[] partitions, bool Recovery, ulong SectorSize)
        {
            SetupHelper.SetupContainers();
            Stream strm = ddfile.ToLower().Contains(@"\\.\physicaldrive") ? new DeviceStream(ddfile) : new FileStream(ddfile, FileMode.Open);

            Disk inDisk = new(strm, Ownership.Dispose);
            Stream contentStream = inDisk.Content;

            Stream fstream = !Recovery ? new EPartitionStream(contentStream, partitions) : contentStream;

            EPartitionStream.GPTPartition[] parts = EPartitionStream.GetPartsFromGPT(fstream);
            foreach (EPartitionStream.GPTPartition part in parts)
            {
                Logging.Log($"{part.Name} - {part.FirstLBA} - {part.LastLBA}");

                _ = fstream.Seek((long)part.FirstLBA, SeekOrigin.Begin);

                using FileStream dst = File.Create(part.Name + ".img");

                var now = DateTime.Now;

                byte[] buffer = new byte[SectorSize];
                for (ulong i = part.FirstLBA; i <= part.LastLBA; i += SectorSize)
                {
                    ShowProgress(i, part.LastLBA, now);
                    _ = fstream.Read(buffer, 0, (int)SectorSize);
                    dst.Write(buffer, 0, (int)SectorSize);
                }
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
                string.Format("{0} {1}MB/s {2:hh\\:mm\\:ss\\.f}", GetDismLikeProgBar((int)(readBytes * 100 / totalBytes)), speed.ToString(),
                    remaining, remaining.TotalHours, remaining.Minutes, remaining.Seconds, remaining.Milliseconds),
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
