using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Ryujinx.Input.Motion
{
    internal class GyroCalibrationFile
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, GyroCalibrationEntry> Entries { get; set; } = new();
    }

    public class GyroCalibrationStore
    {
        private const int CurrentVersion = 2;

        private readonly string _path;
        private readonly object _lock = new();
        private readonly Dictionary<string, GyroCalibrationEntry> _entries = new();
        private DateTime _lastSaveUtc = DateTime.MinValue;

        public GyroCalibrationStore(string path)
        {
            _path = path;
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return;
                }
                using FileStream fs = File.OpenRead(_path);
                var file = JsonSerializer.Deserialize(fs, GyroCalibrationStoreJsonContext.Default.GyroCalibrationFile);
                if (file?.Entries == null)
                {
                    return;
                }
                if (file.Version != CurrentVersion)
                {
                    Logger.Warning?.Print(LogClass.Hid, $"Discarding gyro calibration store (version {file.Version}, current {CurrentVersion}). Recalibrate any controllers you want corrected.");
                    return;
                }
                lock (_lock)
                {
                    _entries.Clear();
                    foreach (var (k, v) in file.Entries)
                    {
                        _entries[k] = v;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning?.Print(LogClass.Hid, $"Failed to load gyro calibration store: {ex.Message}");
            }
        }

        public bool TryGet(string key, out GyroCalibrationEntry entry)
        {
            lock (_lock)
            {
                return _entries.TryGetValue(key, out entry);
            }
        }

        /// <summary>
        /// Fires after <see cref="Set"/> persists an entry. Subscribers can use this to push the
        /// new bias to live calibrators without waiting for a full motion-input reinit.
        /// </summary>
        public event Action<string, GyroCalibrationEntry> EntryUpdated;

        public void Set(string key, GyroCalibrationEntry entry)
        {
            lock (_lock)
            {
                _entries[key] = entry;
            }
            EntryUpdated?.Invoke(key, entry);
        }

        public void Save()
        {
            GyroCalibrationFile snapshot;
            lock (_lock)
            {
                snapshot = new GyroCalibrationFile
                {
                    Version = CurrentVersion,
                    Entries = new Dictionary<string, GyroCalibrationEntry>(_entries),
                };
            }

            try
            {
                string tmp = _path + ".tmp";
                using (FileStream fs = File.Create(tmp))
                {
                    JsonSerializer.Serialize(fs, snapshot, GyroCalibrationStoreJsonContext.Default.GyroCalibrationFile);
                }
                File.Move(tmp, _path, overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Warning?.Print(LogClass.Hid, $"Failed to save gyro calibration store: {ex.Message}");
            }
        }

        public void RequestDebouncedSave(int minIntervalMs)
        {
            DateTime now = DateTime.UtcNow;
            lock (_lock)
            {
                if ((now - _lastSaveUtc).TotalMilliseconds < minIntervalMs)
                {
                    return;
                }
                _lastSaveUtc = now;
            }
            Save();
        }
    }
}
