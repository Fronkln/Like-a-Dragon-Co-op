using DragonEngineLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LADCoop
{
    internal static class TimelineForceNPCPlayer
    {
        private static List<TimelineCastRange> Ranges = new List<TimelineCastRange>();

        struct TimelineCast
        {
            public uint TimelineID;
            public uint SheetID;
            public uint ClockID;

            public bool Check()
            {
                return TimelineManager.CheckClockAchievement(TimelineID, SheetID, ClockID);
            }
        }

        //Inclusive start and end
        struct TimelineCastRange
        {
            public TimelineCast Start;
            public TimelineCast End;
        }

        public static void Read()
        {
            Ranges.Clear();

            string filePath = Path.Combine(Mod.Instance.ModPath, "timeline_force_npc_player_range.txt");

            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                string[] split = line.Split(' ');

                TimelineCastRange range = new TimelineCastRange();
                range.Start = ReadCast(split[0]);
                range.End = ReadCast(split[1]);

                Ranges.Add(range);
            }
        }

        private static TimelineCast ReadCast(string castStr)
        {
            string[] castSplit = castStr.Split('-');

            TimelineCast cast = new TimelineCast();
            cast.TimelineID = Convert.ToUInt32(castSplit[0]);
            cast.SheetID = Convert.ToUInt32(castSplit[1]);
            cast.ClockID = Convert.ToUInt32(castSplit[2]);

            return cast;
        }

        public static bool CheckAny()
        {
            foreach(var range in Ranges)
            {
                if (range.Start.Check())
                {
                    if (!range.End.Check())
                        return true;
                }
            }

            return false;
        }
    }
}
