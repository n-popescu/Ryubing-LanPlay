using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.Input.HLE;
using SDL;
using static SDL.SDL3;
using System;   

namespace Ryujinx.Input.SDL3
{
    /// <summary>
    /// Manages a HID handle of a gamepad to encode and write HD rumble commands for Nin controllers.
    /// </summary>
    public unsafe class NpadHdRumble : IDisposable
    {
        private readonly SDL_hid_device* _hidHandle;
        private readonly ulong _pollRate;
        private static ushort _product;
        
        private int _globalCount;
        private ulong _lastWriteTicks;

        private NpadHdRumble(SDL_hid_device* hidHandle)
        {
            _hidHandle = hidHandle;
            InitializeDevice();
            _pollRate = GetPollRate();
        }

        public static NpadHdRumble Create(SDL_Gamepad* gamepadHandle)
        {
            ushort vendor = SDL_GetGamepadVendor(gamepadHandle);
            if (vendor != 0x057e)
            {
                return null;
            }

            _product = SDL_GetGamepadProduct(gamepadHandle);
            if (!Enum.IsDefined(typeof(HDRumbleSupported), _product))
            {
                return null;
            }

            return new NpadHdRumble(SDL_hid_open(vendor, _product, 0));
        }

        // Some of the code was translated from https://github.com/MIZUSHIKI/JoyShockLibrary-plus-HDRumble
        private bool WriteHdRumble(
            int encLeftLowFreq, int encLeftLowAmp,
            int encLeftHighFreq, int encLeftHighAmp,
            int encRightLowFreq, int encRightLowAmp,
            int encRightHighFreq, int encRightHighAmp)
        {
            byte[] buf = new byte[10];

            buf[0] = 0x10;
            buf[1] = (byte)((++_globalCount) & 0xF);

            buf[2] = (byte)(encLeftHighFreq & 0xFF);
            buf[3] = (byte)(encLeftHighAmp + ((encLeftHighFreq >> 8) & 0xFF));
            buf[4] = (byte)(encLeftLowFreq + ((encLeftLowAmp >> 8) & 0xFF));
            buf[5] = (byte)(encLeftLowAmp & 0xFF);

            buf[6] = (byte)(encRightHighFreq & 0xFF);
            buf[7] = (byte)(encRightHighAmp + ((encRightHighFreq >> 8) & 0xFF));
            buf[8] = (byte)(encRightLowFreq + ((encRightLowAmp >> 8) & 0xFF));
            buf[9] = (byte)(encRightLowAmp & 0xFF);

            if (_globalCount > 0xF)
            {
                _globalCount = 0x0;
            }

            fixed (byte* ptr = buf)
            {
                if (SendHdRumble(ptr, (nuint)buf.Length) >= 0)
                {
                    return true;
                }
                
                if (!String.IsNullOrEmpty(SDL_GetError()))
                {
                    Logger.Error?.PrintMsg(LogClass.Hid, SDL_GetError());
                    SDL_ClearError();
                }
                return false;
            }
        }

        private static int EncodeLowFreq(float lowFreq)
        {
            float lf = Math.Clamp(lowFreq, 40.875885f, 626.286133f);
            return (int) Math.Round(32 * Math.Log2(lf * 0.1f) - 0x40);
        }

        private static int EncodeHighFreq(float highFreq)
        {
            float hf = Math.Clamp(highFreq, 81.75177f, 1252.572266f);
            return (int) Math.Round((32 * Math.Log2(hf * 0.1f) - 0x60) * 4);
        }

        private static int EncodeLowAmp(float rawAmp)
        {
            double encodedAmp = 0;

            if (rawAmp is > 0 and < 0.012f)
            {
                encodedAmp = 1;
            }
            else if (rawAmp is >= 0.012f and < 0.112f)
            {
                encodedAmp = 4 * Math.Log2(rawAmp * 110f);
            }
            else if (rawAmp is >= 0.112f and < 0.225f)
            {
                encodedAmp = 16 * Math.Log2(rawAmp * 17f);
            }
            else if (rawAmp is >= 0.225f and <= 1f)
            {
                encodedAmp = 32 * Math.Log2(rawAmp * 8.7f);
            }

            return (int)Math.Floor(encodedAmp / 2.0) + 64;
        }

        private static int EncodeHighAmp(float rawAmp)
        {
            double encodedAmp = 0;

            if (rawAmp is > 0 and < 0.012f)
            {
                encodedAmp = 1;
            }
            else if (rawAmp is >= 0.012f and < 0.112f)
            {
                encodedAmp = 4 * Math.Log2(rawAmp * 110f);
            }
            else if (rawAmp is >= 0.112f and < 0.225f)
            {
                encodedAmp = 16 * Math.Log2(rawAmp * 17f);
            }
            else if (rawAmp is >= 0.225f and <= 1f)
            {
                encodedAmp = 32 * Math.Log2(rawAmp * 8.7f);
            }

            return (int) Math.Round(encodedAmp * 2);
        }

        public bool HdRumble(VibrationValue left, VibrationValue right)
        {
            return WriteHdRumble(EncodeLowFreq(left.FrequencyLow),
                EncodeLowAmp(left.AmplitudeLow),
                EncodeHighFreq(left.FrequencyHigh),
                EncodeHighAmp(left.AmplitudeHigh),
                EncodeLowFreq(right.FrequencyLow),
                EncodeLowAmp(right.AmplitudeLow),
                EncodeHighFreq(right.FrequencyHigh),
                EncodeHighAmp(right.AmplitudeHigh));
        }

        private int SendHdRumble(byte* data, nuint length)
        {
            int result = 0;
            ulong currentTicks = SDL_GetTicks();

            // Ditch rumble if we haven't hit the poll-rate yet.
            // TODO: figure out a better way to do this
            // While the polling check makes the rumble accurate, it also causes it to miss signals.
            // https://docs.handheldlegend.com/s/progcc-3/doc/lag-comparison-aAR1mV3JLX
            if ((currentTicks - _lastWriteTicks) <= _pollRate)
            {
                return result;
            }
            
            SDL_LockJoysticks();
            {
                // Fun fact: Mario Kart 8 Deluxe sends rumble packets
                // where the amplitude is zero, but the frequency isn't.
                result = SDL_hid_write(_hidHandle, data, length);
                if (result >= 0)
                {
                    _lastWriteTicks = currentTicks;
                }
            }
            SDL_UnlockJoysticks();
            
            return result;
        }

        private void InitializeDevice()
        {
            byte[] init = new byte[64];

            // Pro Controller and Charge Grip
            if (_product 
                is (ushort)HDRumbleSupported.ProController 
                or (ushort)HDRumbleSupported.JoyconGrip)
            {
                init[0] = 0x80;
                init[1] = 0x02;
                
                fixed (byte* ptr = init)
                {
                    SDL_hid_write(_hidHandle, ptr, 64);
                }

                return;
            }
            
            // Joycons
            if (_product 
                is (ushort)HDRumbleSupported.JoyconLeft 
                or (ushort)HDRumbleSupported.JoyconRight
                or (ushort)HDRumbleSupported.JoyconPair)
            {
                init[0] = 0x01;
                init[1] = 0x01;
                init[2] = 0x00;
                init[3] = 0x01;
                init[4] = 0x40;
                init[5] = 0x40;
                init[6] = 0x00;
                init[7] = 0x01;
                init[8] = 0x40;
                init[9] = 0x40;
                
                // TODO: Resend with updated packet for player number LEDs.
                //       And probably move this to NpadDevices.
                init[10] = 0x01; // 0x30
                init[11] = 0x01; // 0x01 0x03 0x07 0x0F
                
                fixed (byte* ptr = init)
                {
                    SDL_hid_write(_hidHandle, ptr, 64);
                }

                return;
            }
            
        }
        
        private ulong GetPollRate()
        {
            byte[] dataArray = new byte[10];
            int read;

            ulong startTime = SDL_GetTicks();
            fixed (byte* ptr = dataArray)
            {
                read = SDL_hid_read(_hidHandle, ptr, 10);
            }
            ulong endTime = SDL_GetTicks();
            
            ulong readTime = endTime - startTime;
            Logger.Debug?.PrintMsg(LogClass.Hid, $"POLL RATE: {readTime}ms.");
            
            if (read == 0)
            {
                return 0;
            }

            return readTime;
        }

        public void Dispose()
        {
            SDL_hid_close(_hidHandle);
        }
    }

    public enum HDRumbleSupported : ushort
    {
        // Currently, HD Rumble only supports the Pro Controller and Joycons.
        // We need to initialize each device differently.
        JoyconLeft = 0x2006,
        JoyconRight = 0x2007,
        JoyconPair = 0x2008,
        ProController = 0x2009,
        JoyconGrip = 0x200e,
        Joycon2Right = 0x2066,
        Joycon2Left = 0x2067,
        Joycon2Pair = 0x2068,
        Switch2ProController = 0x2069,
        GamecubeController = 0x2073
    }
}
