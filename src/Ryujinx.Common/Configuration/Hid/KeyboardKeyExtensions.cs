namespace Ryujinx.Common.Configuration.Hid
{
    public static class KeyboardKeyExtensions
    {
        public static Key ToKey(this PhysicalKey key)
        {
            return key is >= PhysicalKey.Unknown and < PhysicalKey.Count
                ? (Key)(int)key
                : Key.Unknown;
        }

        public static PhysicalKey ToPhysicalKey(this Key key)
        {
            return key is >= Key.Unknown and < Key.Count
                ? (PhysicalKey)(int)key
                : PhysicalKey.Unknown;
        }
    }
}
