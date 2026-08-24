using DragonEngineLibrary;
using LibARMP;
using LibARMP.IO;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LADCoop
{
    internal unsafe class SaveHelper
    {
        private static ArmpTable m_growArmp;

        public static void ResetXP(Player.ID playerID)
        {
            IntPtr dat = SaveData.GetCharaData((uint)playerID);

            if (dat == IntPtr.Zero)
                return;

            IntPtr cExp = dat + 0x28;

            //XP
            Marshal.WriteInt64(cExp, 0);
            //Level
            Marshal.WriteInt64(cExp + 8, 1);
            //Prev EXP
            Marshal.WriteInt64(cExp + 16, 0);
            //Prev Leevl
            Marshal.WriteInt64(cExp + 24, 1);

        }

        public static void ResetJobXP(Player.ID playerID, RPGJobID job)
        {
            //rpg_chara_grow
            if (m_growArmp == null) 
                m_growArmp = GetARMP(2638).GetMainTable();

            if (!m_growArmp.TryGetEntry(playerID.ToString(), out var growEntry))
                return;

            var jobInfoTable = growEntry.GetValueFromColumn<ArmpTable>("job_info_tbl");

            if (jobInfoTable == null)
                return;

            IntPtr party = SaveData.GetItem(23);
            IntPtr learnJob = party + 0x3848;


            foreach (var entry in jobInfoTable.GetAllEntries())
            {
                long offset = (entry.GetValueFromColumn<int>("6") * 24) + 16;
                IntPtr jobSaveEntry = (IntPtr)(learnJob + offset);

                RPGJobID colJob = (RPGJobID)entry.GetValueFromColumn<byte>("1");

                if (colJob != job)
                    continue;

                bool isCurrentJob = job == (RPGJobID)entry.GetValueFromColumn<byte>("1");

                //Level
                if (isCurrentJob)
                {
                    Marshal.WriteByte(jobSaveEntry + 1, 1);
                    Marshal.WriteByte(jobSaveEntry + 9, 1);
                }
                else
                {
                    Marshal.WriteByte(jobSaveEntry + 1, 0);
                    Marshal.WriteByte(jobSaveEntry + 8, 0);
                }
               
                //XP
                Marshal.WriteInt32(jobSaveEntry + 4, 0);
                //Prev XP
                Marshal.WriteInt32(jobSaveEntry + 12, 0);

            }
        }


        private unsafe static void* GetDBBufferPointer(uint id)
        {
            uint val1 = id + (id * 2);

            IntPtr dbPtr = DEService.GetServicePointer(14);

            void* ptr1 = *(void**)(dbPtr + 0x30);
            IntPtr ptr2 = (IntPtr)(*(void**)((long)ptr1 + val1 * 8 + 8));

            if (ptr2 == IntPtr.Zero)
                return (void*)0;

            IntPtr ptr3 = (IntPtr)(*(void**)(ptr2 + 0x50));

            if (ptr3 != IntPtr.Zero)
            {
                IntPtr ptr4 = (IntPtr)((void**)(ptr3 + 0x20));
                return (void*)ptr4;
            }
            else
                return (void*)0;
        }

        public unsafe static ARMP GetARMP(uint index, int bufferSize = 8388608)
        {
            IntPtr ptr = *((IntPtr*)GetDBBufferPointer(index));
            return GetARMPWithPointer(ptr, bufferSize);
        }

        public unsafe static ARMP GetARMPWithPointer(IntPtr pointer, int bufferSize = 8388608)
        {
            byte[] buffer = new byte[bufferSize];
            Marshal.Copy(pointer, buffer, 0, bufferSize);

            UnmanagedMemoryStream stream = new UnmanagedMemoryStream((byte*)pointer, bufferSize);

            try
            {
                return ArmpFileReader.ReadARMP(buffer, baseARMPMemoryAddress: pointer);
            }
            catch (Exception ex)
            {
                throw new Exception("Armp read error:\n" + ex.ToString());
            }
        }
    }
}
