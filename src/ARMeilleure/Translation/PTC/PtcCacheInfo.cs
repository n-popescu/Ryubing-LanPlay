namespace ARMeilleure.Translation.PTC
{
    public readonly struct PtcCacheInfo
    {
        public ulong ProcessId { get; }
        public string TitleIdText { get; }
        public string ApplicationIdText { get; }
        public byte ProgramIndex { get; }
        public string DisplayVersion { get; }
        public string ProcessKind { get; }
        public string CacheSelector { get; }

        public string CacheKey => $"{DisplayVersion}-{CacheSelector}";

        public PtcCacheInfo(
            ulong processId,
            string titleIdText,
            string applicationIdText,
            byte programIndex,
            string displayVersion,
            string processKind,
            string cacheSelector)
        {
            ProcessId = processId;
            TitleIdText = titleIdText ?? string.Empty;
            ApplicationIdText = applicationIdText ?? string.Empty;
            ProgramIndex = programIndex;
            DisplayVersion = displayVersion ?? string.Empty;
            ProcessKind = processKind ?? string.Empty;
            CacheSelector = string.IsNullOrEmpty(cacheSelector) ? "default" : cacheSelector;
        }
    }
}
