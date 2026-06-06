using ARMeilleure.State;
using Ryujinx.Common.Logging;
using Ryujinx.Memory;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Cpu.AppleHv
{
    [SupportedOSPlatform("macos")]
    class HvExecutionContextVcpu : IHvExecutionContext
    {
        private static readonly MemoryBlock _setSimdFpRegFuncMem;
        private delegate HvResult SetSimdFpReg(ulong vcpu, HvSimdFPReg reg, in V128 value, nint funcPtr);
        private static readonly SetSimdFpReg _setSimdFpReg;
        private static readonly nint _setSimdFpRegNativePtr;

        public static bool AggressiveMode { get; set; } = false;

        public ulong ThreadUid { get; set; }

        private readonly ulong[] _x = new ulong[32];
        private readonly V128[] _v = new V128[32];

        private ulong _pc;
        private ulong _elrEl1;
        private ulong _esrEl1;
        private ulong _tpidrEl0;
        private ulong _tpidrroEl0;
        private ulong _fpcr;
        private ulong _fpsr;
        private ulong _pstateRaw;

        private long _fallbackCount;
        private long _lastWarningTicks;
        private const long WarningCooldownTicks = 1_000_000_000;

        private readonly ulong _vcpu;
        private int _interruptRequested;

        static HvExecutionContextVcpu()
        {
            _setSimdFpRegFuncMem = new MemoryBlock(MemoryBlock.GetPageSize());
            _setSimdFpRegFuncMem.Write(0, 0x3DC00040u);
            _setSimdFpRegFuncMem.Write(4, 0xD61F0060u);
            _setSimdFpRegFuncMem.Reprotect(0, _setSimdFpRegFuncMem.Size, MemoryPermission.ReadAndExecute);

            _setSimdFpReg = Marshal.GetDelegateForFunctionPointer<SetSimdFpReg>(_setSimdFpRegFuncMem.Pointer);

            if (NativeLibrary.TryLoad(HvApi.LibraryName, out nint hvLibHandle))
            {
                _setSimdFpRegNativePtr = NativeLibrary.GetExport(hvLibHandle, nameof(HvApi.hv_vcpu_set_simd_fp_reg));
            }
        }

        public HvExecutionContextVcpu(ulong vcpu)
        {
            _vcpu = vcpu;
            Reset();
        }

        public void Reset()
        {
            InitializeCacheDefaults();
            _fallbackCount = 0;
            _lastWarningTicks = 0;
            _interruptRequested = 0;
        }

        private void InitializeCacheDefaults()
        {
            _pstateRaw = 0x80000000UL;
            _pc = 0;
            _elrEl1 = 0;
            _esrEl1 = 0;
            _tpidrEl0 = 0;
            _tpidrroEl0 = 0;
            _fpcr = 0;
            _fpsr = 0;
            Array.Clear(_x, 0, _x.Length);
            Array.Clear(_v, 0, _v.Length);
        }

        private void LogHvWarning(string message)
        {
            if (AggressiveMode) return;

            long now = DateTime.UtcNow.Ticks;
            if (now - _lastWarningTicks > WarningCooldownTicks)
            {
                Logger.Warning?.Print(LogClass.Cpu, $"[AppleHv] {message} | Total fallbacks: {_fallbackCount}");
                _lastWarningTicks = now;
            }
        }

        public ulong Pc { get => GetRegCached(HvReg.PC, ref _pc); set => SetRegCached(HvReg.PC, value, ref _pc); }
        public ulong ElrEl1 { get => GetSysRegCached(HvSysReg.ELR_EL1, ref _elrEl1); set => SetSysRegCached(HvSysReg.ELR_EL1, value, ref _elrEl1); }
        public ulong EsrEl1 { get => GetSysRegCached(HvSysReg.ESR_EL1, ref _esrEl1); set => SetSysRegCached(HvSysReg.ESR_EL1, value, ref _esrEl1); }
        public long TpidrEl0 { get => (long)GetSysRegCached(HvSysReg.TPIDR_EL0, ref _tpidrEl0); set => SetSysRegCached(HvSysReg.TPIDR_EL0, (ulong)value, ref _tpidrEl0); }
        public long TpidrroEl0 { get => (long)GetSysRegCached(HvSysReg.TPIDRRO_EL0, ref _tpidrroEl0); set => SetSysRegCached(HvSysReg.TPIDRRO_EL0, (ulong)value, ref _tpidrroEl0); }

        public uint Pstate
        {
            get
            {
                HvResult res = HvApi.hv_vcpu_get_reg(_vcpu, HvReg.CPSR, out ulong val);
                if (res == HvResult.BadArgument)
                {
                    _fallbackCount++;
                    LogHvWarning("PAC failure on CPSR");
                    return (uint)_pstateRaw;
                }
                res.ThrowOnError();
                _pstateRaw = val;
                return (uint)val;
            }
            set
            {
                HvResult res = HvApi.hv_vcpu_set_reg(_vcpu, HvReg.CPSR, value);
                if (res != HvResult.BadArgument) res.ThrowOnError();
                _pstateRaw = value;
            }
        }

        public uint Fpcr { get => (uint)GetRegCached(HvReg.FPCR, ref _fpcr); set => SetRegCached(HvReg.FPCR, value, ref _fpcr); }
        public uint Fpsr { get => (uint)GetRegCached(HvReg.FPSR, ref _fpsr); set => SetRegCached(HvReg.FPSR, value, ref _fpsr); }

        public ulong GetX(int index)
        {
            ulong value;

            if (index == 31)
            {
                HvResult res = HvApi.hv_vcpu_get_sys_reg(_vcpu, HvSysReg.SP_EL0, out value);
                if (res == HvResult.BadArgument)
                {
                    _fallbackCount++;
                    LogHvWarning("PAC failure on SP_EL0");
                    return _x[31];
                }
                res.ThrowOnError();
                return _x[31] = value;
            }

            if ((uint)index > 30) return 0;

            HvResult resX = HvApi.hv_vcpu_get_reg(_vcpu, HvReg.X0 + (uint)index, out value);
            if (resX == HvResult.BadArgument)
            {
                _fallbackCount++;
                if (_fallbackCount % 128 == 0)
                    LogHvWarning($"PAC failure on X{index}");
                return _x[index];
            }
            resX.ThrowOnError();
            return _x[index] = value;
        }

        public void SetX(int index, ulong value)
        {
            if (index == 31)
            {
                HvResult res = HvApi.hv_vcpu_set_sys_reg(_vcpu, HvSysReg.SP_EL0, value);
                if (res != HvResult.BadArgument) res.ThrowOnError();
                _x[31] = value;
            }
            else if ((uint)index <= 30)
            {
                HvResult res = HvApi.hv_vcpu_set_reg(_vcpu, HvReg.X0 + (uint)index, value);
                if (res != HvResult.BadArgument) res.ThrowOnError();
                _x[index] = value;
            }
        }

        public V128 GetV(int index)
        {
            if ((uint)index > 31) return V128.Zero;

            HvResult res = HvApi.hv_vcpu_get_simd_fp_reg(_vcpu, HvSimdFPReg.Q0 + (uint)index, out HvSimdFPUchar16 val);
            if (res == HvResult.BadArgument)
            {
                _fallbackCount++;
                return _v[index];
            }
            res.ThrowOnError();
            return _v[index] = new V128(val.Low, val.High);
        }

        public void SetV(int index, V128 value)
        {
            if ((uint)index > 31) return;

            HvResult res = _setSimdFpReg(_vcpu, HvSimdFPReg.Q0 + (uint)index, value, _setSimdFpRegNativePtr);
            if (res != HvResult.BadArgument) res.ThrowOnError();
            _v[index] = value;
        }

        private ulong GetRegCached(HvReg reg, ref ulong cached)
        {
            HvResult res = HvApi.hv_vcpu_get_reg(_vcpu, reg, out ulong val);
            if (res == HvResult.BadArgument)
            {
                _fallbackCount++;
                return cached;
            }
            res.ThrowOnError();
            return cached = val;
        }

        private void SetRegCached(HvReg reg, ulong value, ref ulong cached)
        {
            HvResult res = HvApi.hv_vcpu_set_reg(_vcpu, reg, value);
            if (res != HvResult.BadArgument) res.ThrowOnError();
            cached = value;
        }

        private ulong GetSysRegCached(HvSysReg reg, ref ulong cached)
        {
            HvResult res = HvApi.hv_vcpu_get_sys_reg(_vcpu, reg, out ulong val);
            if (res == HvResult.BadArgument)
            {
                _fallbackCount++;
                return cached;
            }
            res.ThrowOnError();
            return cached = val;
        }

        private void SetSysRegCached(HvSysReg reg, ulong value, ref ulong cached)
        {
            HvResult res = HvApi.hv_vcpu_set_sys_reg(_vcpu, reg, value);
            if (res != HvResult.BadArgument) res.ThrowOnError();
            cached = value;
        }

        public void RequestInterrupt()
        {
            if (Interlocked.Exchange(ref _interruptRequested, 1) == 0)
            {
                ulong vcpu = _vcpu;
                HvApi.hv_vcpus_exit(ref vcpu, 1);
            }
        }

        public bool GetAndClearInterruptRequested()
        {
            return Interlocked.Exchange(ref _interruptRequested, 0) != 0;
        }

        public long GetFallbackCount() => _fallbackCount;
    }
}