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
    internal unsafe static class CharacterInput
    {
        public static PXDStaticVector* Slots;

        private delegate void AttachPad(IntPtr entity, ref uint result, uint mode, ref bool bnew, IntPtr ccharacter, IntPtr args);
        private delegate void DetachPad(IntPtr entity, ushort slot);
        private delegate void PushSlot(IntPtr inputService);

        private static AttachPad func_AttachPad;
        private static PushSlot func_PushSlot;

        public static IntPtr CoopBindingManager;
        private static IntPtr accessCoopBindingManagerFuncPtr;

        static CharacterInput()
        {
            IntPtr inputService = DEService.GetServicePointer(ServiceID.input);
            Slots = (PXDStaticVector*)(inputService + 0x38);

            func_AttachPad = Marshal.GetDelegateForFunctionPointer<AttachPad>(CPP.ReadCall(CPP.PatternSearch("E8 ? ? ? ? 33 FF 8B DF 8B CF 48 8D 55 40")));
            func_PushSlot = Marshal.GetDelegateForFunctionPointer<PushSlot>(CPP.ReadCall(CPP.PatternSearch("E8 ? ? ? ? BA ? ? ? ? 48 8B CB E8 ? ? ? ? C6 05")));

            //Two new input slots to support up to 3 co-op players
            func_PushSlot(inputService);
            func_PushSlot(inputService);

            //Initialize the slots with device ID or we will crash the second we try to use them
            IntPtr slot3 = Slots->ElementAt<IntPtr>(2);
            IntPtr slot4 = Slots->ElementAt<IntPtr>(3);
            Marshal.WriteInt32(slot3 + 0xEC, 2);
            Marshal.WriteInt32(slot4 + 0xEC, 3);

            //1) Allocate space for a function
            //2) Allocate space for our own "binding" manager, which will be used to support up to 4 players instead of 2
            //3) Patch addresses to use our binding manager insteads
            IntPtr bindingManagerAccessFuncCall1 = CPP.PatternSearch("E8 ? ? ? ? 8B 94 98");
            accessCoopBindingManagerFuncPtr = CPP.AllocBuffer(bindingManagerAccessFuncCall1);
            CoopBindingManager = CPP.AllocBuffer(bindingManagerAccessFuncCall1);

            CPP.PatchMemory(accessCoopBindingManagerFuncPtr, 0x48, 0xB8);
            CPP.PatchMemory(accessCoopBindingManagerFuncPtr + 2, BitConverter.GetBytes((long)CoopBindingManager));
            CPP.PatchMemory(accessCoopBindingManagerFuncPtr + 10, 0xC3, 0x90);

            byte[] bindDat = new byte[0x1A0];

            Marshal.Copy(NativeFunctions.GetSteamBindingManager(), bindDat, 0, 0x1A0);
            Marshal.Copy(bindDat, 0, CoopBindingManager, 0x1A0);

            //Initialize the player device IDs with default values
            Marshal.WriteInt32(CoopBindingManager + 0xE0, 0);
            Marshal.WriteInt32(CoopBindingManager + 0xE0 + 4, 1);
            Marshal.WriteInt32(CoopBindingManager + 0xE0 + 8, 2);
            Marshal.WriteInt32(CoopBindingManager + 0xE0 + 12, 3);

            CPP.WriteCall(bindingManagerAccessFuncCall1, accessCoopBindingManagerFuncPtr);
            CPP.WriteRelativeAddress(CPP.PatternSearch("48 89 05 ? ? ? ? 8B 98 ? ? ? ? 89 5D"), CoopBindingManager, 7);

            DragonEngine.Log("Coop Binding Manager Access Func: " + accessCoopBindingManagerFuncPtr.ToString("X"));
            DragonEngine.Log("Coop Binding Manager: " + CoopBindingManager.ToString("X"));
        }

        public static void DetachInputCommand(Character character)
        {
            ECControllerPadViewLocal pad = character.GetComponent<ECControllerPadViewLocal>(ECSlotID.controller_pad_view_local);

            if (pad.IsValid())
            {
                pad.PadListener.DeviceSlot = Slots->ElementAt<IntPtr>(0);

                if (pad.DestroyComponent(false, true))
                    character.EntityComponentMap.Erase(ECSlotID.controller_pad_view_local);
            }
        }

        public static void SetSlot(Character character, int index)
        {
            EntityComponentHandle<ECControllerPadViewLocal> componentHandle = character.GetComponent<ECControllerPadViewLocal>(ECSlotID.controller_pad_view_local);

            if (!componentHandle.IsValid())
                return;

            ECControllerPadViewLocal component = componentHandle.Get();

            if(index >= Slots->ElementSize)
            {
                DragonEngine.Log("Tried to assign slot ID beyond the bounds: " + index);
                return;
            }

            component.PadListener.DeviceSlot = Slots->ElementAt<IntPtr>(index);
        }

        public static void SetEveryoneToSlot(int index)
        {
            if (index >= Slots->ElementSize)
            {
                DragonEngine.Log("Tried to assign slot ID beyond the bounds: " + index);
                return;
            }

            for(uint i = 0; i < 4; i++)
            {
                var nakamaChara = NakamaManager.GetCharacterHandle(i);

                if (!nakamaChara.IsValid())
                    continue;

                SetSlot(nakamaChara.Get(), index);
            }
        }

        public static void SetEveryoneToSlotAndDevice(int playerIndex)
        {
            if (playerIndex >= Slots->ElementSize)
            {
                DragonEngine.Log("Tried to assign slot ID beyond the bounds: " + playerIndex);
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                var nakamaChara = NakamaManager.GetCharacterHandle(i);

                if (!nakamaChara.IsValid())
                    continue;

                SetSlot(nakamaChara.Get(), playerIndex);
                SetDeviceForSteamPlayer(i, GetDeviceIDForPlayer(playerIndex));
            }
        }

        public static void SetDeviceForSteamPlayer(int playerID, int deviceID)
        {
            if(playerID <= 1)
                Marshal.WriteInt32(NativeFunctions.GetSteamBindingManager() + 0xE0 + (4 * (int)playerID), deviceID);

            Marshal.WriteInt32(CoopBindingManager + 0xE0 + (4 * playerID), deviceID);
        }


        public static bool Player1IsKBD()
        {
            return Mod.Player1IsKBD; 
        }

        public static int GetDeviceIDForPlayer(int playerIdx)
        {
            if (IniSettings.DeviceIDsOverride[playerIdx] != -1)
                return IniSettings.DeviceIDsOverride[playerIdx];

            bool p1IsKbd = Player1IsKBD();

            if (playerIdx == 0)
            {
                if (p1IsKbd)
                    return 7;
                else
                    return 0;
            }
            else
            {
                if (p1IsKbd)
                    return playerIdx - 1;
                else
                    return playerIdx;
            }
 
        }
    }
}
