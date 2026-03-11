using DragonEngineLibrary;
using DragonEngineLibrary.Unsafe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LADCoop
{
    internal static class NativeFunctions
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate void RegistrationFighter(IntPtr fman, int findex, ref uint charaHandle, int groupID);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public delegate IntPtr GetSteamBindngMan();

        private static RegistrationFighter _registrationFighter;
        public static GetSteamBindngMan GetSteamBindingManager { get; private set; }

        public static void Init()
        {
            //FighterManager function that turns a character into a fighter
            _registrationFighter = Marshal.GetDelegateForFunctionPointer<RegistrationFighter>(CPP.ReadCall(CPP.PatternSearch("E8 ? ? ? ? 49 89 9C 2E")));
            GetSteamBindingManager = Marshal.GetDelegateForFunctionPointer<GetSteamBindngMan>(CPP.ReadCall(CPP.PatternSearch("E8 ? ? ? ? 33 DB 4C 8B B8")));
        }

        public static void RegisterNakamaForBattle(int index)
        {
            var nakamaCharacterHandle = NakamaManager.GetCharacterHandle((uint)index).UID;

            if (nakamaCharacterHandle <= 0)
                return;

            _registrationFighter(FighterManager.Pointer(), index, ref nakamaCharacterHandle, 2);
        }
    }
}
