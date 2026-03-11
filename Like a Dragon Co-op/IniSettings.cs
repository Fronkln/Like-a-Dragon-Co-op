using DragonEngineLibrary;
using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LADCoop
{
    internal static class IniSettings
    {
        public static Player.ID[] NonPresentPlayerCharacterPreference = new Player.ID[3]
        {
            Player.ID.adachi,
            Player.ID.nanba,
            Player.ID.saeko,
        };

        public static int[] DeviceIDsOverride = new int[4]
        {
            -1,
            -1,
            -1,
            -1
        };

        public static void Read()
        {
            Ini settings = new Ini(Path.Combine(Mod.Instance.ModPath, "settings.ini"));
            
            Mod.CoopPlayersCount = int.Parse(settings.GetValue("CoopPlayersCount", "", "1"));
            Mod.ControlMode = int.Parse(settings.GetValue("ControlMode", "", "0"));
            Mod.Player1IsKBD = settings.GetValue("Player1IsKeyboard", "", "1") == "1";
            Mod.TeleportDistance = float.Parse(settings.GetValue("TeleportDistance", "", "20"), CultureInfo.InvariantCulture);


            NonPresentPlayerCharacterPreference[0] = (Player.ID)Enum.Parse(typeof(Player.ID), settings.GetValue("NotPresentPlayer2Character", "", "adachi"));
            NonPresentPlayerCharacterPreference[1] = (Player.ID)Enum.Parse(typeof(Player.ID), settings.GetValue("NotPresentPlayer3Character", "", "nanba"));
            NonPresentPlayerCharacterPreference[2] = (Player.ID)Enum.Parse(typeof(Player.ID), settings.GetValue("NotPresentPlayer4Character", "", "saeko"));

            DeviceIDsOverride[0] = int.Parse(settings.GetValue("Player1InputOverride", "", "-1"));
            DeviceIDsOverride[1] = int.Parse(settings.GetValue("Player2InputOverride", "", "-1"));
            DeviceIDsOverride[2] = int.Parse(settings.GetValue("Player3InputOverride", "", "-1"));
            DeviceIDsOverride[3] = int.Parse(settings.GetValue("Player4InputOverride", "", "-1"));
        }
    }
}
