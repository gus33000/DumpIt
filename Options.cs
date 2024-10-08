﻿// Copyright (c) 2018, Gustave M. - gus33000.me - @gus33000
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

namespace DumpIt
{
    [Verb("request-dump", isDefault: true, HelpText = "")]
    internal class Options
    {
        [Option('i', "img-file", HelpText = @"A path to the img file to convert *OR* a PhysicalDisk path. i.e. \\.\PhysicalDrive1", Required = true)]
        public required string ImgFile
        {
            get; set;
        }

        [Option('v', "vhdx-file", HelpText = "A path to the VHDX file to output", Required = true)]
        public required string VhdxFile
        {
            get; set;
        }

        [Option('e', "excluded-file", HelpText = "A path to the file with all partitions to exclude", Required = false, Default = ".\\provisioning-partitions.txt")]
        public required string ExcludedFile
        {
            get; set;
        }

        [Option('r', "enable-recoveryvhd", Required = false, HelpText = "Generates a recovery vhd with no partition skipped. Useful for clean state restore for a SPECIFIC unique device.", Default = false)]
        public bool Recovery
        {
            get; set;
        }
    }
}
