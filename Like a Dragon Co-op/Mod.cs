using DragonEngineLibrary;
using DragonEngineLibrary.Service;
using DragonEngineLibrary.Unsafe;
using MinHook;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LADCoop
{
    internal unsafe class Mod : DragonEngineMod
    {
        public static Mod Instance;

        public static int ControlMode = 0;
        public static int CoopPlayersCount = 3;
        public static float TeleportDistance = 15f;
        public static bool Player1IsKBD = false;
        public static bool EnforceHumanFreeze = false;

        private static IntPtr* PadManager;
        private static HookEngine HookEngine = new HookEngine();

        public static EntityHandle<Character> HumanPlayer;
        public static bool IsBattle = false;
        public static bool IsRealtime = false;

        private bool m_loading = false;

        public override void OnModInit()
        {
            base.OnModInit();

            Instance = this;

            IniSettings.Read();
            TimelineForceNPCPlayer.Read();

            NativeFunctions.Init();

            PadManager = (IntPtr*)CPP.ResolveRelativeAddress(CPP.PatternSearch("48 89 35 ? ? ? ? 48 85 FF"), 7);

            m_origChara = HookEngine.CreateHook<Delegates.CreateChara>(CPP.ReadCall(CPP.PatternSearch("E8 ? ? ? ? 90 C7 84 24 ? ? ? ? ? ? ? ? 48 8D 05 ? ? ? ? 49 89 06")), create_entity_with_uid_chara);
            m_origSetupAttachComponent = HookEngine.CreateHook<Delegates.SetupAttachComponent>(CPP.PatternSearch("40 55 56 57 41 56 41 57 48 8B EC 48 83 EC ? 48 C7 45 ? ? ? ? ? 48 89 9C 24 ? ? ? ? 48 8B F1 45 33 C9 45 33 C0 B2"), setup_attach_component);
            m_origCharaReqStartFighter = HookEngine.CreateHook<Delegates.CharacterRequestStartFighter>(CPP.ReadCall(CPP.PatternSearch("E8 ? ? ? ? 48 8B 0D ? ? ? ? E8 ? ? ? ? B0")), request_start_fighter);
            m_origFighterIsAlly = HookEngine.CreateHook<Delegates.FighterIsAlly>(CPP.PatternSearch("40 53 48 83 EC ? 48 8B 01 48 8B D9 80 B8 ? ? ? ? ? 0F 85"), Fighter_IsAlly);
            m_origCharacterComponentsPreFinalize = HookEngine.CreateHook<Delegates.CharacterComponentsPreFinalize>(CPP.ReadCall(CPP.PatternSearch("E8 ? ? ? ? 48 8D 55 ? 48 8D 4F ? E8 ? ? ? ? 41 8B CF")), CCharacterComponents_PreFinalize);
            
            
            if(EnforceHumanFreeze)
                m_origCharaHandleHumanFreeze = HookEngine.CreateHook<Delegates.CharacterHandleHumanFreeze>(CPP.PatternSearch("40 57 48 83 EC ? 48 C7 44 24 ? ? ? ? ? 48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 48 8B F2 48 8B E9 48 81 C1"), handle_human_freeze);

            IsRealtime = Parless.GetModsList().Any(x => x.Contains("BrawlerKiwami", StringComparison.OrdinalIgnoreCase));
            var autoModeHookAddr = CPP.PatternSearch("48 89 5C 24 ? 48 89 74 24 ? 57 48 83 EC ? 41 8B 40 ? 49 8B F8");

            //Like a Brawler controls this itself
            if (autoModeHookAddr != IntPtr.Zero && IsRealtime)
                m_handleAutoModeTrampoline = HookEngine.CreateHook<Delegates.TurnCommandDecideManagerHandleAutoMode>(autoModeHookAddr, TurnCommandDecideManager_HandleAutoMode);

            HookEngine.EnableHooks();

            DragonEngine.RegisterJob(Update, DEJob.Update, true);

#if DEBUG
            Thread thread = new Thread(InputThread);
            thread.Start();
#endif

            if (CoopPlayersCount > 3)
                CoopPlayersCount = 3;

            DragonEngine.Log("Heroes of Yokohama initialized. Is realtime combat: " + IsRealtime);
        }

#if DEBUG
        internal static void InputThread()
        {
            while (true)
            {
                if (DragonEngine.IsKeyHeld(VirtualKey.LeftControl))
                    if (DragonEngine.IsKeyDown(VirtualKey.A))
                        SaveHelper.ResetJobXP(Player.ID.adachi, RPGJobID.adachi_01);
            }
        }
#endif

        private void Update()
        {
            bool loading = GameVarManager.GetValueBool(GameVarID.is_ui_loading) || GameVarManager.GetValueBool(GameVarID.is_loading_image_displaying);

            if (!m_loading)
            {
                if (loading)
                    m_loading = true;
            }
            else
            {
                if (!loading)
                {
                    m_loading = false;
                    OnLoadComplete();
                }
            }

            HumanPlayer = DragonEngine.GetHumanPlayer();
            var humanPlayerCharacter = HumanPlayer.Get();


            bool preBattleStart = IsBattle;
            IsBattle = HumanPlayer.IsValid() && FighterManager.GetFighter(0).IsValid();

            if(preBattleStart != IsBattle)
            {
                if (IsBattle)
                    OnBattleStart();
            }

            if (!IsBattle)
                UpdateSlots();
            else
            {
                UpdateSlotsBattle();
                UpdateBattle();
            }

            for (int i = 1; i < 4; i++)
            {
                if (!IsNakamaIndexPlayer(i))
                    continue;

                var nakamaHandle = NakamaManager.GetCharacterHandle(i);

                if (!nakamaHandle.IsValid())
                    continue;

                var nakama = nakamaHandle.Get();

                float distToMainPlayer = Vector3.Distance(humanPlayerCharacter.Transform.Position, nakama.Transform.Position);

                if(!IsBattle || IsRealtime)
                    if (distToMainPlayer >= TeleportDistance)
                        nakamaHandle.Get().RequestWarpPose(new PoseInfo(humanPlayerCharacter.Transform.Position, 0));


                if (!IsBattle)
                {
                    //Reset Camera in adventure
                    if (BattleTurnManager.CurrentPhase == BattleTurnManager.TurnPhase.NumPhases)
                    {
                        if (nakama.Pad.IsJustPush(BattleButtonID.npc))
                            nakama.RequestWarpPose(new PoseInfo(humanPlayerCharacter.Transform.Position, 0));
                    }
                }
            }
        }

        private void OnBattleStart()
        {
            var mainPlayer = NakamaManager.GetCharacterHandle(0).Get();

            for (int i = 1; i < 4; i++)
            {
                if (!IsNakamaIndexPlayer(i))
                    continue;

                var nakamaHandle = NakamaManager.GetCharacterHandle(i);

                if (!nakamaHandle.IsValid())
                    continue;

                var nakama = nakamaHandle.Get();

                if(Vector3.Distance(nakama.Transform.Position, mainPlayer.Transform.Position) >= 6)
                        nakama.RequestWarpPose(new PoseInfo(mainPlayer.Transform.Position, 0));
            }
        }

        private void UpdateBattle()
        {
            for (int i = 1; i < 4; i++)
            {
                if (!IsNakamaIndexPlayer(i))
                    continue;

                var nakamaHandle = NakamaManager.GetCharacterHandle(i);

                if (!nakamaHandle.IsValid())
                    continue;

                var nakama = nakamaHandle.Get();

#warning This is better off in Like a Dragon Revised source code.

                if (!IsRealtime)
                {
                    if (!IsMyTurn(nakama))
                        CharacterInput.DetachInputCommand(nakama);
                }
            }
        }

        private void OnLoadComplete()
        {
            if (NakamaManager.GetCharacterHandle(0).IsValid())
            {
                if (Party.GetMainMemberCount() - 1 < CoopPlayersCount)
                {
                    DragonEngine.Log("We are short party members. Force creating them on Scene ID: " + SceneService.CurrentScene.Get().SceneID);
                    CreateMissingPlayers();
                }
            }
        }

        private void CreateMissingPlayers()
        {
            int mainMemberCount = Party.GetMainMemberCount();
            int presentPartyMemberCount = mainMemberCount - 1;
            int amountToMake = CoopPlayersCount - presentPartyMemberCount;

            List<Player.ID> presentMembers = new List<Player.ID>();
            List<Player.ID> characterPool = new List<Player.ID>()
            {
                Player.ID.adachi,
                Player.ID.nanba,
                Player.ID.saeko,
                Player.ID.jyungi,
                Player.ID.chou,
                Player.ID.woman_a
            };

            for (int i = 0; i < 4; i++)
            {
                var presentPlayer = Party.GetMainMember(i);

                if (presentPlayer != Player.ID.invalid)
                    presentMembers.Add(presentPlayer);
            }

            foreach (var member in presentMembers)
                characterPool.Remove(member);

            int startingPlayerIndex = presentPartyMemberCount + 1; // 4 - presentPartyMemberCount - amountToMake;

            Player.ID mainPlayer = Party.GetMainMember(0);

            IntPtr partyChunk = SaveData.GetItem(23);

            for (int i = 0; i < amountToMake; i++, startingPlayerIndex++)
            {
                Player.ID preferredPlayer = IniSettings.NonPresentPlayerCharacterPreference[startingPlayerIndex - 1];

                if (!characterPool.Contains(preferredPlayer))
                    preferredPlayer = characterPool.First();

                Party.InsertMember(preferredPlayer, startingPlayerIndex);
                characterPool.Remove(preferredPlayer);

                //Player did not exist in savedata at all before. Initialize to Kasuga's stats.
                if (Player.GetJobLevel(preferredPlayer) == 99 && Player.GetLevel(preferredPlayer) == 99)
                {
                    SaveHelper.ResetXP(preferredPlayer);

                    for (int k = 1; k < (int)RPGJobID.num; k++)
                    {
                        var job = (RPGJobID)k;
                        Player.SetJobLevel(job, 1, preferredPlayer, false, false);
                        SaveHelper.ResetJobXP(preferredPlayer, job);
                    }

                }

            }
        }

        //Any nakama treated as player right now
        public static bool AnyNakamaPlayersPresent()
        {
            for (uint i = 1; i < 4; i++)
            {
                var nakama = NakamaManager.GetCharacterHandle(i);

                if (!nakama.IsValid())
                    continue;

                if (nakama.Get().Attributes.is_player)
                    return true;
            }

            return false;
        }

        private void UpdateSlots()
        {
            for (int i = 0; i < 4; i++)
            {
                var nakama = NakamaManager.GetCharacterHandle(i);

                if (nakama.IsValid())
                {
                    if (IsNakamaIndexPlayer(i))
                    {
                        if (!ShouldResetSlots())
                            CharacterInput.SetSlot(nakama, i);
                        else
                            CharacterInput.SetSlot(nakama, 0);


                        CharacterInput.SetDeviceForSteamPlayer(i, CharacterInput.GetDeviceIDForPlayer(i));
                    }

                    Marshal.WriteByte(*PadManager + 16, 1);
                }
            }
        }

        private void UpdateSlotsBattle()
        {
            if (ShouldResetSlots())
            {
                CharacterInput.SetEveryoneToSlotAndDevice(0);
                //ResetSlots();
                return;
            }

            if (IsRealtime)
            {
                UpdateSlots();
                return;
            }

            if (BattleTurnManager.CurrentActionStep >= BattleTurnManager.ActionStep.Init)
            {
                Fighter selectedAttacker = BattleTurnManager.SelectedFighter.Get().GetFighter();

                var playerID = selectedAttacker.Character.Attributes.player_id;

                if (playerID != 0)
                {
                    var nakamaIdx = NakamaManager.FindIndex(playerID);

                    if (nakamaIdx != -1)
                    {
                        CharacterInput.SetEveryoneToSlotAndDevice(nakamaIdx);

                        if (ControlMode == 0 || IsNakamaIndexPlayer(nakamaIdx))
                            CharacterInput.SetEveryoneToSlotAndDevice(nakamaIdx);
                        else if (ControlMode == 1)
                        {
                            if (nakamaIdx == 2)
                                CharacterInput.SetEveryoneToSlotAndDevice(0);
                            else if (nakamaIdx == 3)
                                CharacterInput.SetEveryoneToSlotAndDevice(1);
                        }
                    }

                }
                else
                {
                    if (selectedAttacker.IsEnemy())
                    {
                        var target = BattleTurnManager.TargetFighter;
                        var targetFighter = target.Get().GetFighter();


                        if (targetFighter.IsPlayer() || targetFighter.IsAlly())
                        {
                            var targetPlayerID = targetFighter.Character.Attributes.player_id;

                            if (targetPlayerID > 0)
                            {
                                int nakamaIdx = NakamaManager.FindIndex(targetFighter.Character.Attributes.player_id);

                                if (IsNakamaIndexPlayer(nakamaIdx))
                                    CharacterInput.SetEveryoneToSlotAndDevice(nakamaIdx);
                                else
                                    CharacterInput.SetEveryoneToSlotAndDevice(0);
                            }

                        }
                    }
                }
            }

            if (BattleTurnManager.CurrentPhase >= BattleTurnManager.TurnPhase.Cleanup || GameVarManager.GetValueBool(GameVarID.is_hact))
                CharacterInput.SetEveryoneToSlotAndDevice(0);
        }

        private static bool ShouldResetSlots()
        {
            bool pausedGame = GameVarManager.GetValueBool(GameVarID.is_pause) || GameVarManager.GetValueBool(GameVarID.is_ui_loading);
            bool battleInitializing = BattleTurnManager.CurrentPhase >= BattleTurnManager.TurnPhase.StartWait && BattleTurnManager.CurrentPhase < BattleTurnManager.TurnPhase.Action;

            if (pausedGame || battleInitializing)
                return true;

            return false;
        }

        private static bool ShouldSpawnEveryoneAsNonPlayer()
        {
            return TimelineForceNPCPlayer.CheckAny();
        }

        private static void ResetSlots()
        {
            CharacterInput.SetEveryoneToSlot(0);
        }

        public static bool IsNakamaIndexPlayer(int idx)
        {
            if (idx <= CoopPlayersCount)
                return true;

            return false;
        }

        public static bool IsMyTurn(Character chara)
        {
            return BattleTurnManager.SelectedFighter.UID == chara.UID;
        }


        //Sad but necessary: Hooked to ccharacter constructor instead of create entity
        //Because it can crash for some people, weird hooking quirk.
        private static Delegates.CreateChara m_origChara;
        private static unsafe IntPtr create_entity_with_uid_chara(IntPtr cchara, IntPtr uid_arg, uint* parent, IntPtr createParam, int _file_port)
        {
            byte* attributesPtr = (byte*)(createParam + 0x30);
            Player.ID playerID = (Player.ID)Marshal.ReadInt32((IntPtr)(attributesPtr + 0x28C));

            if (playerID != 0)
            {
                int nakamaIndex = NakamaManager.FindIndex(playerID);

                if (nakamaIndex > 0)
                {
                    if (IsNakamaIndexPlayer(nakamaIndex) && !ShouldSpawnEveryoneAsNonPlayer())
                    {
                        Utils.SetPlayerModeCharacterAttributes(attributesPtr);
                    }
                }
            }

            return m_origChara(cchara, uid_arg, parent, createParam, _file_port);
        }

#warning THERE HAS TO BE A BETTER WAY TO DO THIS, JUST ENSURE IS PLAYER IS NOT SET BACK TO ZERO?

        private static Delegates.SetupAttachComponent m_origSetupAttachComponent;
        private static unsafe void setup_attach_component(IntPtr charaSetup)
        {
            Character chara = new Character() { Pointer = (IntPtr)(*(long*)charaSetup) };
            Player.ID playerID = chara.Attributes.player_id;


            if (playerID > 0)
            {
                if (!ShouldSpawnEveryoneAsNonPlayer())
                {

                    int nakamaIndex = NakamaManager.FindIndex(playerID);

                    if (IsNakamaIndexPlayer(nakamaIndex))
                        Utils.SetPlayerModeCharacterAttributes((byte*)(chara.Pointer + 0x1F0));
                }
            }

            m_origSetupAttachComponent(charaSetup);
        }

        private static Delegates.CharacterRequestStartFighter m_origCharaReqStartFighter;
        private static unsafe void request_start_fighter(IntPtr characterPtr)
        {
            Character chara = new Character() { Pointer = characterPtr };

            m_origCharaReqStartFighter(characterPtr);

            if (chara.UID == HumanPlayer.UID)
            {
                for (uint i = 1; i < 4; i++)
                {
                    var nakamaChara = NakamaManager.GetCharacterHandle(i);

                    if (!nakamaChara.IsValid())
                        continue;

                    if (IsNakamaIndexPlayer((int)i) && nakamaChara.Get().Attributes.is_player)
                    {
                        NativeFunctions.RegisterNakamaForBattle((int)i);
                        DragonEngine.Log("Registered co-op player nakama index " + i);
                    }
                }
            }
        }


        private static Delegates.CharacterHandleHumanFreeze m_origCharaHandleHumanFreeze;
        private static unsafe void handle_human_freeze(IntPtr characterPtr, IntPtr padInfo)
        {
            Character chara = new Character() { Pointer = characterPtr };

            if (chara.UID == NakamaManager.GetCharacterHandle(0).UID)
            {
                m_origCharaHandleHumanFreeze(characterPtr, padInfo);

                //Handle human freeze manually for co-op players due to how they have to be created.
                for (uint i = 1; i < 4; i++)
                {
                    var nakamaChara = NakamaManager.GetCharacterHandle(i);

                    if (!nakamaChara.IsValid())
                        continue;

                    var nakamaObj = nakamaChara.Get();
                    m_origCharaHandleHumanFreeze(nakamaObj.Pointer, nakamaObj.Pointer + 0x1250);
                }
            }
        }



        //Purpose: Ensure co-op players are still treated as "Ally" in battle to ensure no weirdness happens.
        private static Delegates.FighterIsAlly m_origFighterIsAlly;
        private static unsafe bool Fighter_IsAlly(IntPtr fighterPtr)
        {
            Fighter fighter = new Fighter(fighterPtr);
            var playerID = fighter.Character.Attributes.player_id;

            if (playerID > 0 && fighter.Character.UID != HumanPlayer.UID)
            {
                int nakamaIndex = NakamaManager.FindIndex(playerID);

                if (IsNakamaIndexPlayer(nakamaIndex))
                    return true;
                else
                    return m_origFighterIsAlly(fighterPtr);
            }
            else
                return m_origFighterIsAlly(fighterPtr);
        }

        //Purpose: Prevent crashes by resetting the slot of a pad listener and ensure the co-op player is disposed proper.
        private static Delegates.CharacterComponentsPreFinalize m_origCharacterComponentsPreFinalize;
        private static unsafe IntPtr CCharacterComponents_PreFinalize(IntPtr characterCompsPtr)
        {
            var chara = new Character() { Pointer = *(IntPtr*)characterCompsPtr };
            var playerID = chara.Attributes.player_id;

            if (playerID > 0)
            {
                if (chara.EntityComponentMap.GetComponent(ECSlotID.controller_pad_view_local).IsValid())
                    CharacterInput.SetSlot(chara, 0);
            }
            IntPtr res = m_origCharacterComponentsPreFinalize(characterCompsPtr);

            if (playerID > 0)
            {
                //NECEESSARY for the proper cleanup of the co-op player!
                //Without it, they don't get properly transitioned into scripted battles etc...
                //We basically set the "is_player" boolean on the coop player false after cleaning up the entity
                byte* attributesPtr = (byte*)(chara.Pointer + 0x1F0);
                attributesPtr[0x277] = 0;
            }

            return res;
        }

        //Purpose: Automode for npc party members if on control mode 0
        private static Delegates.TurnCommandDecideManagerHandleAutoMode m_handleAutoModeTrampoline = null;
        private static bool TurnCommandDecideManager_HandleAutoMode(IntPtr thisPtr, IntPtr selectCommandInfo, long** fighterPtrPtr)
        {
            Fighter fighter = new Fighter((IntPtr)(*fighterPtrPtr));

            int* autoModePtr = (int*)fighterPtrPtr + 2;
            int ogAutoMode = *autoModePtr;

            if (ControlMode <= 0)
            {
                if (fighter.IsAlly())
                {
                    var playerID = fighter.Character.Attributes.player_id;

                    if (playerID <= 0)
                        *autoModePtr = 3;
                    else
                    {
                        var nakamaIndex = NakamaManager.FindIndex(playerID);

                        if (!IsNakamaIndexPlayer(nakamaIndex))
                            *autoModePtr = 3;
                        else
                            *autoModePtr = 0;
                    }
                }
                else if (fighter.IsPlayer())
                    *autoModePtr = 0;
            }

            return m_handleAutoModeTrampoline(thisPtr, selectCommandInfo, fighterPtrPtr);
        }

    }
}
