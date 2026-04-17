using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Caps.Types;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Caps
{
    class CaptureManager
    {
        private readonly string _sdCardPath;

        private uint _shimLibraryVersion;

        public CaptureManager(Switch device)
        {
            _sdCardPath = FileSystem.VirtualFileSystem.GetSdCardPath();
        }

        public ResultCode SetShimLibraryVersion(ServiceCtx context)
        {
            ulong shimLibraryVersion = context.RequestData.ReadUInt64();
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            ulong appletResourceUserId = context.RequestData.ReadUInt64();
#pragma warning restore IDE0059

            // TODO: Service checks if the pid is present in an internal list and returns ResultCode.BlacklistedPid if it is.
            //       The list contents needs to be determined.

            ResultCode resultCode = ResultCode.OutOfRange;

            if (shimLibraryVersion != 0)
            {
                if (_shimLibraryVersion == shimLibraryVersion)
                {
                    resultCode = ResultCode.Success;
                }
                else if (_shimLibraryVersion != 0)
                {
                    resultCode = ResultCode.ShimLibraryVersionAlreadySet;
                }
                else if (shimLibraryVersion == 1)
                {
                    resultCode = ResultCode.Success;

                    _shimLibraryVersion = 1;
                }
            }

            return resultCode;
        }

        public ResultCode SaveScreenShot(byte[] screenshotData, ulong appletResourceUserId, ulong titleId, out ApplicationAlbumEntry applicationAlbumEntry)
        {
            applicationAlbumEntry = default;

            if (screenshotData.Length == 0)
            {
                return ResultCode.NullInputBuffer;
            }

            /*
            // NOTE: On our current implementation, appletResourceUserId starts at 0, disable it for now.
            if (appletResourceUserId == 0)
            {
                return ResultCode.InvalidArgument;
            }
            */

            /*
            // Doesn't occur in our case.
            if (applicationAlbumEntry == null)
            {
                return ResultCode.NullOutputBuffer;
            }
            */

            if (screenshotData.Length >= 0x384000)
            {
                DateTime currentDateTime = DateTime.Now;

                applicationAlbumEntry = new ApplicationAlbumEntry()
                {
                    Size = (ulong)Unsafe.SizeOf<ApplicationAlbumEntry>(),
                    TitleId = titleId,
                    AlbumFileDateTime = new AlbumFileDateTime()
                    {
                        Year = (ushort)currentDateTime.Year,
                        Month = (byte)currentDateTime.Month,
                        Day = (byte)currentDateTime.Day,
                        Hour = (byte)currentDateTime.Hour,
                        Minute = (byte)currentDateTime.Minute,
                        Second = (byte)currentDateTime.Second,
                        UniqueId = 0,
                    },
                    AlbumStorage = AlbumStorage.Sd,
                    ContentType = ContentType.Screenshot,
                    Padding = new Array5<byte>(),
                    Unknown0x1f = 1,
                };

                // NOTE: The hex hash is a HMAC-SHA256 (first 32 bytes) using a hardcoded secret key over the titleId, we can simulate it by hashing the titleId instead.
                string hash = Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(titleId)))[..0x20];
                string folderPath = Path.Combine(_sdCardPath, "Nintendo", "Album", currentDateTime.Year.ToString("00"), currentDateTime.Month.ToString("00"), currentDateTime.Day.ToString("00"));
                string filePath = GenerateFilePath(folderPath, applicationAlbumEntry, currentDateTime, hash);

                // TODO: Handle that using the FS service implementation and return the right error code instead of throwing exceptions.
                Directory.CreateDirectory(folderPath);

                while (File.Exists(filePath))
                {
                    applicationAlbumEntry.AlbumFileDateTime.UniqueId++;

                    filePath = GenerateFilePath(folderPath, applicationAlbumEntry, currentDateTime, hash);
                }

                try
                {
                    // NOTE: The saved JPEG file doesn't have the limitation in the extra EXIF data.
                    using SKBitmap bitmap = new(new SKImageInfo(1280, 720, SKColorType.Rgba8888, SKAlphaType.Premul));
                    ReadOnlySpan<byte> rawImage = screenshotData;
                    Span<byte> pixelBuffer = bitmap.GetPixelSpan();
                    int strideBytes = bitmap.RowBytes;

                    if (rawImage.Length < strideBytes * bitmap.Info.Height)
                    {
                        throw new Exception("Screenshot buffer is too small.");
                    }

                    for (int i = 0, l = bitmap.Info.Height; i < l; ++i)
                    {
                        ReadOnlySpan<byte> srcRow = rawImage.Slice(i * strideBytes, strideBytes);
                        Span<byte> dstRow = pixelBuffer.Slice(i * strideBytes, strideBytes);

                        srcRow.CopyTo(dstRow);
                    }

                    using SKData data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 80);
                    using FileStream file = File.OpenWrite(filePath);
                    data.SaveTo(file);

                    return ResultCode.Success;
                }
                catch (Exception e)
                {
                    Logger.Error?.Print(LogClass.ServiceCaps, $"Failed to save screenshot: {e}\nmessage: {e.Message}.");
                    return ResultCode.NullInputBuffer;
                }
            }

            return ResultCode.NullInputBuffer;
        }

        private string GenerateFilePath(string folderPath, ApplicationAlbumEntry applicationAlbumEntry, DateTime currentDateTime, string hash)
        {
            string fileName = $"{currentDateTime:yyyyMMddHHmmss}{applicationAlbumEntry.AlbumFileDateTime.UniqueId:00}-{hash}.jpg";

            return Path.Combine(folderPath, fileName);
        }
    }
}
