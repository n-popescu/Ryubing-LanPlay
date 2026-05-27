using System.Collections.Generic;

namespace Ryujinx.Input.Motion
{
    public static class GyroCalibrationStoreProvider
    {
        private static GyroCalibrationStore _instance;
        private static readonly object _lock = new();
        private static readonly HashSet<string> _activeKeys = new();
        private static readonly object _activeLock = new();

        public static void Initialize(string path)
        {
            lock (_lock)
            {
                _instance ??= new GyroCalibrationStore(path);
            }
        }

        public static GyroCalibrationStore Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance;
                }
            }
        }

        public static bool RegisterActiveKey(string key, string controllerName)
        {
            lock (_activeLock)
            {
                if (_activeKeys.Add(key))
                {
                    return true;
                }
                Ryujinx.Common.Logging.Logger.Warning?.Print(
                    Ryujinx.Common.Logging.LogClass.Hid,
                    $"Cannot uniquely identify controller {controllerName}; gyro calibration will be shared with another connected device of the same model. (key: {key})");
                return false;
            }
        }

        public static void UnregisterActiveKey(string key)
        {
            lock (_activeLock)
            {
                _activeKeys.Remove(key);
            }
        }
    }
}
