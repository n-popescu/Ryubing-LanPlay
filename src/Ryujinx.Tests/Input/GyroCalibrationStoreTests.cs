using NUnit.Framework;
using Ryujinx.Input.Motion;
using System;
using System.IO;
using System.Numerics;

namespace Ryujinx.Tests.Input
{
    [TestFixture]
    public class GyroCalibrationStoreTests
    {
        private string _tmpPath;

        [SetUp]
        public void SetUp()
        {
            _tmpPath = Path.Combine(Path.GetTempPath(), $"gyro_calibration_test_{Guid.NewGuid()}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tmpPath))
            {
                File.Delete(_tmpPath);
            }
        }

        [Test]
        public void NewStore_HasNoEntries()
        {
            var store = new GyroCalibrationStore(_tmpPath);
            Assert.That(store.TryGet("any-key", out _), Is.False);
        }

        [Test]
        public void Set_And_TryGet_RoundTripsValues()
        {
            var store = new GyroCalibrationStore(_tmpPath);
            var entry = new GyroCalibrationEntry
            {
                Name = "Pro Controller",
                Bias = new Vector3(0.4f, -0.2f, 0.1f),
                CalibratedAt = DateTime.UtcNow,
                Source = GyroCalibrationSource.Manual,
            };

            store.Set("guid:serial", entry);

            Assert.That(store.TryGet("guid:serial", out var fetched), Is.True);
            Assert.That(fetched.Bias.X, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(fetched.Source, Is.EqualTo(GyroCalibrationSource.Manual));
        }

        [Test]
        public void Save_Then_Load_PreservesEntries()
        {
            var store = new GyroCalibrationStore(_tmpPath);
            store.Set("guid:serial", new GyroCalibrationEntry
            {
                Name = "Pro Controller",
                Bias = new Vector3(1f, 2f, 3f),
                CalibratedAt = new DateTime(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc),
                Source = GyroCalibrationSource.Passive,
            });
            store.Save();

            var reopened = new GyroCalibrationStore(_tmpPath);
            Assert.That(reopened.TryGet("guid:serial", out var entry), Is.True);
            Assert.That(entry.Bias, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(entry.Source, Is.EqualTo(GyroCalibrationSource.Passive));
        }

        [Test]
        public void Load_CorruptFile_RecoversAsEmpty()
        {
            File.WriteAllText(_tmpPath, "{ this is not json");

            var store = new GyroCalibrationStore(_tmpPath);

            Assert.That(store.TryGet("anything", out _), Is.False);
        }

        [Test]
        public void Set_FiresEntryUpdated_WithMatchingKeyAndEntry()
        {
            var store = new GyroCalibrationStore(_tmpPath);
            string capturedKey = null;
            GyroCalibrationEntry capturedEntry = null;
            store.EntryUpdated += (k, e) => { capturedKey = k; capturedEntry = e; };

            var entry = new GyroCalibrationEntry { Bias = new Vector3(0.4f, -0.2f, 0.1f) };
            store.Set("guid:serial", entry);

            Assert.That(capturedKey, Is.EqualTo("guid:serial"));
            Assert.That(capturedEntry, Is.SameAs(entry));
        }

        [Test]
        public void RequestDebouncedSave_FirstCallSavesImmediately()
        {
            var store = new GyroCalibrationStore(_tmpPath);
            store.Set("k", new GyroCalibrationEntry { Bias = new Vector3(1, 2, 3) });

            store.RequestDebouncedSave(minIntervalMs: 60_000);

            Assert.That(File.Exists(_tmpPath), Is.True);
        }

        [Test]
        public void RequestDebouncedSave_WithinInterval_DoesNotSaveAgain()
        {
            var store = new GyroCalibrationStore(_tmpPath);
            store.Set("k", new GyroCalibrationEntry { Bias = new Vector3(1, 2, 3) });
            store.RequestDebouncedSave(minIntervalMs: 60_000);

            DateTime firstWrite = File.GetLastWriteTimeUtc(_tmpPath);
            System.Threading.Thread.Sleep(50);

            store.Set("k", new GyroCalibrationEntry { Bias = new Vector3(4, 5, 6) });
            store.RequestDebouncedSave(minIntervalMs: 60_000);

            Assert.That(File.GetLastWriteTimeUtc(_tmpPath), Is.EqualTo(firstWrite));
        }
    }
}
