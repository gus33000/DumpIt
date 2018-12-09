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

namespace DumpIt
{
    internal class Constants
    {
        internal const ulong SectorSize = 0x200;

        internal readonly static string[] partitions = new string[]
        {
            "DPP",
            "MODEM_FSG",
            "MODEM_FS1",
            "MODEM_FS2",
            "MODEM_FSC",
            "DDR",
            "SEC",
            "APDP",
            "MSADP",
            "DPO",
            "SSD",
            "UEFI_BS_NV",
            "UEFI_NV",
            "UEFI_RT_NV",
            "UEFI_RT_NV_RPMB",
            "BOOTMODE",
            "LIMITS",
            "BACKUP_BS_NV",
            "BACKUP_SBL1",
            "BACKUP_SBL2",
            "BACKUP_SBL3",
            "BACKUP_PMIC",
            "BACKUP_DBI",
            "BACKUP_UEFI",
            "BACKUP_RPM",
            "BACKUP_QSEE",
            "BACKUP_QHEE",
            "BACKUP_TZ",
            "BACKUP_HYP",
            "BACKUP_WINSECAPP",
            "BACKUP_TZAPPS",
            "SVRawDump"
        };
    }
}
