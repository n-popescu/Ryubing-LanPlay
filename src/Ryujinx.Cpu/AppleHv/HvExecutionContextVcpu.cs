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

        public ulong ThreadUid { get; set; }

        // Shadow cache
        private readonly ulong[] _x = new ulong[32];
        private readonly V128[] _v = new V128[32];
        private ulong _pc;
        private uint _pstate;
        private ulong _elrEl1;
        private ulong _esrEl1;
        private ulong _tpidrEl0;
        private ulong _tpidrroEl0;
        private ulong _fpcr;
        private ulong _fpsr;

        private bool _cacheInitialized;
        private long _lastWarningTicks;
        private const long WarningCooldownTicks = 500_000_000; // ~0.5s

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

        private readonly ulong _vcpu;
        private int _interruptRequested;

        public HvExecutionContextVcpu(ulong vcpu)
        {
            _vcpu = vcpu;
            InitializeCacheDefaults();
        }

        private void InitializeCacheDefaults()
        {
            _pstate = 0x80000000;
            _cacheInitialized = true;
        }

        private void LogHvWarning(string message)
        {
            var now = DateTime.UtcNow.Ticks;
            if (now - _lastWarningTicks > WarningCooldownTicks)
            {
                Logger.Warning?.Print(LogClass.Cpu, message);
                _lastWarningTicks = now;
            }
        }

        public ulong Pc
        {
            get => GetRegCached(HvReg.PC, ref _pc);
            set => SetRegCached(HvReg.PC, value, ref _pc);
        }

        public ulong ElrEl1
        {
            get => GetSysRegCached(HvSysReg.ELR_EL1, ref _elrEl1);
            set => SetSysRegCached(HvSysReg.ELR_EL1, value, ref _elrEl1);
        }

        public ulong EsrEl1
        {
            get => GetSysRegCached(HvSysReg.ESR_EL1, ref _esrEl1);
            set => SetSysRegCached(HvSysReg.ESR_EL1, value, ref _esrEl1);
        }

        public long TpidrEl0
        {
            get => (long)GetSysRegCached(HvSysReg.TPIDR_EL0, ref _tpidrEl0);
            set => SetSysRegCached(HvSysReg.TPIDR_EL0, (ulong)value, ref _tpidrEl0);
        }

        public long TpidrroEl0
        {
            get => (long)GetSysRegCached(HvSysReg.TPIDRRO_EL0, ref _tpidrroEl0);
            set => SetSysRegCached(HvSysReg.TPIDRRO_EL0, (ulong)value, ref _tpidrroEl0);
        }

        public uint Pstate
        {
            get
            {
                var resP = HvApi.hv_vcpu_get_reg(_vcpu, HvReg.CPSR, out ulong valP);
                if (resP == HvResult.BadArgument) return _pstate;
                resP.ThrowOnError();
                _pstate = (uint)valP;
                return _pstate;
            }
            set
            {
                var resP = HvApi.hv_vcpu_set_reg(_vcpu, HvReg.CPSR, value);
                if (resP != HvResult.BadArgument) resP.ThrowOnError();
                _pstate = value;
            }
        }

        public uint Fpcr
        {
            get => (uint)GetRegCached(HvReg.FPCR, ref _fpcr);
            set => SetRegCached(HvReg.FPCR, value, ref _fpcr);
        }

        public uint Fpsr
        {
            get => (uint)GetRegCached(HvReg.FPSR, ref _fpsr);
            set => SetRegCached(HvReg.FPSR, value, ref _fpsr);
        }

        public ulong GetX(int index)
        {
            if (index == 31)
            {
                var resSp = HvApi.hv_vcpu_get_sys_reg(_vcpu, HvSysReg.SP_EL0, out ulong valSp);
                if (resSp == HvResult.BadArgument) return _x[31];
                resSp.ThrowOnError();
                _x[31] = valSp;
                return valSp;
            }

            if (index < 0 || index > 30) return 0;

            var resX = HvApi.hv_vcpu_get_reg(_vcpu, HvReg.X0 + (uint)index, out ulong valX);
            if (resX == HvResult.BadArgument) return _x[index];
            resX.ThrowOnError();
            _x[index] = valX;
            return valX;
        }

        public void SetX(int index, ulong value)
        {
            if (index == 31)
            {
                var resSp = HvApi.hv_vcpu_set_sys_reg(_vcpu, HvSysReg.SP_EL0, value);
                if (resSp != HvResult.BadArgument) resSp.ThrowOnError();
                _x[31] = value;
            }
            else if (index >= 0 && index <= 30)
            {
                var resX = HvApi.hv_vcpu_set_reg(_vcpu, HvReg.X0 + (uint)index, value);
                if (resX != HvResult.BadArgument) resX.ThrowOnError();
                _x[index] = value;
            }
        }

        public V128 GetV(int index)
        {
            if (index < 0 || index > 31) return V128.Zero;

            var resV = HvApi.hv_vcpu_get_simd_fp_reg(_vcpu, HvSimdFPReg.Q0 + (uint)index, out HvSimdFPUchar16 simdVal);
            if (resV == HvResult.BadArgument) return _v[index];
            if (resV != HvResult.Success) resV.ThrowOnError();

            var vec = new V128(simdVal.Low, simdVal.High);
            _v[index] = vec;
            return vec;
        }

        public void SetV(int index, V128 value)
        {
            if (index < 0 || index > 31) return;

            var resV = _setSimdFpReg(_vcpu, HvSimdFPReg.Q0 + (uint)index, value, _setSimdFpRegNativePtr);
            if (resV != HvResult.BadArgument) resV.ThrowOnError();
            _v[index] = value;
        }

        private ulong GetRegCached(HvReg reg, ref ulong cached)
        {
            var resG = HvApi.hv_vcpu_get_reg(_vcpu, reg, out ulong valG);
            if (resG == HvResult.BadArgument) return cached;
            resG.ThrowOnError();
            cached = valG;
            return valG;
        }

        private void SetRegCached(HvReg reg, ulong value, ref ulong cached)
        {
            var resS = HvApi.hv_vcpu_set_reg(_vcpu, reg, value);
            if (resS != HvResult.BadArgument) resS.ThrowOnError();
            cached = value;
        }

        private ulong GetSysRegCached(HvSysReg reg, ref ulong cached)
        {
            var resSys = HvApi.hv_vcpu_get_sys_reg(_vcpu, reg, out ulong valSys);
            if (resSys == HvResult.BadArgument) return cached;
            resSys.ThrowOnError();
            cached = valSys;
            return valSys;
        }

        private void SetSysRegCached(HvSysReg reg, ulong value, ref ulong cached)
        {
            var resSys = HvApi.hv_vcpu_set_sys_reg(_vcpu, reg, value);
            if (resSys != HvResult.BadArgument) resSys.ThrowOnError();
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
    }
}