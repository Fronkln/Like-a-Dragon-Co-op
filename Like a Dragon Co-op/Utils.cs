namespace LADCoop
{
    internal unsafe static class Utils
    {
        public static void SetPlayerModeCharacterAttributes(byte* attributesPtr)
        {
            attributesPtr[0x277] = 1;
            attributesPtr[0x278] = 0;
            attributesPtr[0x279] = 0;
        }

        public static void SetNPCModeCharacterAttributes(byte* attributesPtr)
        {
            attributesPtr[0x277] = 0;
            attributesPtr[0x278] = 1;
            attributesPtr[0x279] = 0;
        }
    }
}
