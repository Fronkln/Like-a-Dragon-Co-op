using DragonEngineLibrary;
using System;
using System.Runtime.InteropServices;

namespace LADCoop
{
    internal unsafe static class Delegates
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public delegate IntPtr CreateChara(IntPtr cchara,IntPtr uid_arg,uint* parent,IntPtr _create_param,int _file_port);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public delegate void SetupAttachComponent(IntPtr charaSetup);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public delegate void CharacterRequestStartFighter(IntPtr characterPtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.U1)]
        public delegate bool FighterIsAlly(IntPtr fighterPtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public delegate IntPtr CharacterComponentsPreFinalize(IntPtr characterCompPtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public delegate bool TurnCommandDecideManagerHandleAutoMode(IntPtr thisPtr, IntPtr selectCommandInfo, long** fighterPtrPtr);
    }
}
