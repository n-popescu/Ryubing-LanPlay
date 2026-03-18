using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Input
{
    public static class PhysicalKeyExtensions
    {
        public static Key ToInputKey(this ConfigPhysicalKey key)
        {
            return key is >= ConfigPhysicalKey.Unknown and < ConfigPhysicalKey.Count
                ? (Key)(int)key
                : Key.Unknown;
        }
    }
}
