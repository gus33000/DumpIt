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
using CommandLine.Text;
using DiscUtils;
using DiscUtils.Containers;
using DiscUtils.Raw;
using DiscUtils.Streams;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DumpIt
{
    class Program
    {
        internal static string[] partitions = Constants.partitions;

        static void Main(string[] args)
        {
            var ass = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(ass.Location);
            var Heading = new HeadingInfo(fvi.FileDescription, ass.GetName().Version.ToString());
            var Copyright = new CopyrightInfo(fvi.CompanyName, DateTime.Today.Year);

            Parser.Default.ParseArguments<Options>(args).WithParsed(o =>
            {
                Console.WriteLine(Heading.ToString());
                Console.WriteLine(Copyright.ToString());
                Console.WriteLine();
                
                if (!string.IsNullOrEmpty(o.Excludelist) && File.Exists(o.Excludelist))
                    partitions = new List<string>(File.ReadAllLines(o.Excludelist)).ToArray();
                
                ulong eMMCDumpSize;
                ulong SectorSize = 0x200;

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

                Logging.Log("Reported source device eMMC size is: " + eMMCDumpSize + " bytes - " + eMMCDumpSize / 1024 / 1024 + "MB - " + eMMCDumpSize / 1024 / 1024 / 1024 + "GB.");
                Logging.Log("Selected " + SectorSize + "B for the sector size");
                
                ConvertDD2VHD(o.ImgFile, o.VhdFile, partitions, o.Recovery, (int)SectorSize);
            });
        }

        /// <summary>
        ///     Coverts a raw DD image into a VHD file suitable for FFU imaging.
        /// </summary>
        /// <param name="ddfile">The path to the DD file.</param>
        /// <param name="vhdfile">The path to the output VHD file.</param>
        /// <returns></returns>
        public static void ConvertDD2VHD(string ddfile, string vhdfile, string[] partitions, bool Recovery, int SectorSize)
        {
            SetupHelper.SetupContainers();
            Stream strm;

            if (ddfile.ToLower().Contains(@"\\.\physicaldrive"))
                strm = new DeviceStream(ddfile);
            else
                strm = new FileStream(ddfile, FileMode.Open);

            EPartitionStream.GPTPartition[] parts = EPartitionStream.GetPartsFromGPT(strm);
            foreach (EPartitionStream.GPTPartition part in parts)
            {
                Console.WriteLine(string.Concat(new string[]
                {
                    part.Name,
                    " - ",
                    part.FirstLBA.ToString(),
                    " - ",
                    part.LastLBA.ToString()
                }));
                strm.Seek((long)part.FirstLBA, SeekOrigin.Begin);
                using (FileStream dst = File.Create(part.Name + ".img"))
                {
                    byte[] buffer = new byte[4096L];
                    for (ulong i = part.FirstLBA; i <= part.LastLBA; i += 4096UL)
                    {
                        Console.Title = i.ToString() + "/" + part.LastLBA.ToString();
                        strm.Read(buffer, 0, 4096);
                        dst.Write(buffer, 0, 4096);
                    }
                }
            }
        }
        
        protected static void ShowProgress(long totalBytes, DateTime startTime, object sourceObject,
            PumpProgressEventArgs e)
        {
            var now = DateTime.Now;
            var timeSoFar = now - startTime;

            var remaining =
                TimeSpan.FromMilliseconds(timeSoFar.TotalMilliseconds / e.BytesRead * (totalBytes - e.BytesRead));

            var speed = Math.Round(e.SourcePosition / 1024L / 1024L / timeSoFar.TotalSeconds);

            Logging.Log(
                string.Format("{0} {1}MB/s {2:hh\\:mm\\:ss\\.f}", GetDismLikeProgBar((int)(e.BytesRead * 100 / totalBytes)), speed.ToString(),
                    remaining, remaining.TotalHours, remaining.Minutes, remaining.Seconds, remaining.Milliseconds),
                returnline: false);

        }

        private static string GetDismLikeProgBar(int perc)
        {
            var eqsLength = (int)((double)perc / 100 * 55);
            var bases = new string('=', eqsLength) + new string(' ', 55 - eqsLength);
            bases = bases.Insert(28, perc + "%");
            if (perc == 100)
                bases = bases.Substring(1);
            else if (perc < 10)
                bases = bases.Insert(28, " ");
            return "[" + bases + "]";
        }
        
        internal class Options
        {
            [Option('i', "img-file", HelpText = @"A path to the img file to convert *OR* a PhysicalDisk path. i.e. \\.\PhysicalDrive1", Required = true)]
            public string ImgFile { get; set; }

            [Option('v', "vhd-file", HelpText = "A path to the VHD file to output", Required = true)]
            public string VhdFile { get; set; }
            
            [Option('e', "exclude-list", Required = false,
                HelpText = "Path to an optional partition exclude text list to use instead of the builtin one.")]
            public string Excludelist { get; set; }
            
            [Option('r', "enable-recoveryvhd", Required = false, HelpText = "Generates a recovery vhd with no partition skipped. Useful for clean state restore for a SPECIFIC unique device.", Default = false)]
            public bool Recovery { get; set; }
        }
    }
}
