using ARMeilleure.State;
using Ryujinx.Memory;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Ryujinx.Common.Logging;

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

        static HvExecutionContextVcpu()
        {
            // .NET does not support passing vectors by value, so we need to pass a pointer and use a native
            // function to load the value into a vector register.
            _setSimdFpRegFuncMem = new MemoryBlock(MemoryBlock.GetPageSize());
            _setSimdFpRegFuncMem.Write(0, 0x3DC00040u); // LDR Q0, [X2]
            _setSimdFpRegFuncMem.Write(4, 0xD61F0060u); // BR X3
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
        }

        public ulong Pc
        {
            get
            {
                var result = HvApi.hv_vcpu_get_reg(_vcpu, HvReg.PC, out ulong pc);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on PC"); return 0; }
                result.ThrowOnError();
                return pc;
            }
            set
            {
                var result = HvApi.hv_vcpu_set_reg(_vcpu, HvReg.PC, value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on PC (set)"); return; }
                result.ThrowOnError();
            }
        }

        public ulong ElrEl1
        {
            get
            {
                var result = HvApi.hv_vcpu_get_sys_reg(_vcpu, HvSysReg.ELR_EL1, out ulong elr);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on ELR_EL1"); return 0; }
                result.ThrowOnError();
                return elr;
            }
            set
            {
                var result = HvApi.hv_vcpu_set_sys_reg(_vcpu, HvSysReg.ELR_EL1, value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on ELR_EL1 (set)"); return; }
                result.ThrowOnError();
            }
        }

        public ulong EsrEl1
        {
            get
            {
                var result = HvApi.hv_vcpu_get_sys_reg(_vcpu, HvSysReg.ESR_EL1, out ulong esr);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on ESR_EL1"); return 0; }
                result.ThrowOnError();
                return esr;
            }
            set
            {
                var result = HvApi.hv_vcpu_set_sys_reg(_vcpu, HvSysReg.ESR_EL1, value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on ESR_EL1 (set)"); return; }
                result.ThrowOnError();
            }
        }

        public long TpidrEl0
        {
            get
            {
                var result = HvApi.hv_vcpu_get_sys_reg(_vcpu, HvSysReg.TPIDR_EL0, out ulong val);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on TPIDR_EL0"); return 0; }
                result.ThrowOnError();
                return (long)val;
            }
            set
            {
                var result = HvApi.hv_vcpu_set_sys_reg(_vcpu, HvSysReg.TPIDR_EL0, (ulong)value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on TPIDR_EL0 (set)"); return; }
                result.ThrowOnError();
            }
        }

        public long TpidrroEl0
        {
            get
            {
                var result = HvApi.hv_vcpu_get_sys_reg(_vcpu, HvSysReg.TPIDRRO_EL0, out ulong val);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on TPIDRRO_EL0"); return 0; }
                result.ThrowOnError();
                return (long)val;
            }
            set
            {
                var result = HvApi.hv_vcpu_set_sys_reg(_vcpu, HvSysReg.TPIDRRO_EL0, (ulong)value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on TPIDRRO_EL0 (set)"); return; }
                result.ThrowOnError();
            }
        }

        public uint Pstate
        {
            get
            {
                var result = HvApi.hv_vcpu_get_reg(_vcpu, HvReg.CPSR, out ulong cpsr);
                if (result == HvResult.BadArgument)
                {
                    Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on CPSR (Pstate) - common during early GetThreadContext3");
                    return 0;
                }
                result.ThrowOnError();
                return (uint)cpsr;
            }
            set
            {
                var result = HvApi.hv_vcpu_set_reg(_vcpu, HvReg.CPSR, (ulong)value);
                if (result == HvResult.BadArgument)
                {
                    Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on CPSR (set)");
                    return;
                }
                result.ThrowOnError();
            }
        }

        public uint Fpcr
        {
            get
            {
                var result = HvApi.hv_vcpu_get_reg(_vcpu, HvReg.FPCR, out ulong val);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on FPCR"); return 0; }
                result.ThrowOnError();
                return (uint)val;
            }
            set
            {
                var result = HvApi.hv_vcpu_set_reg(_vcpu, HvReg.FPCR, (ulong)value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on FPCR (set)"); return; }
                result.ThrowOnError();
            }
        }

        public uint Fpsr
        {
            get
            {
                var result = HvApi.hv_vcpu_get_reg(_vcpu, HvReg.FPSR, out ulong val);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on FPSR"); return 0; }
                result.ThrowOnError();
                return (uint)val;
            }
            set
            {
                var result = HvApi.hv_vcpu_set_reg(_vcpu, HvReg.FPSR, (ulong)value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on FPSR (set)"); return; }
                result.ThrowOnError();
            }
        }

        public ulong GetX(int index)
        {
            if (index == 31)
            {
                var result = HvApi.hv_vcpu_get_sys_reg(_vcpu, HvSysReg.SP_EL0, out ulong value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on SP_EL0 (X31)"); return 0; }
                result.ThrowOnError();
                return value;
            }
            else if (index >= 0 && index <= 30)
            {
                var result = HvApi.hv_vcpu_get_reg(_vcpu, HvReg.X0 + (uint)index, out ulong value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, $"HV_BAD_ARGUMENT on X{index}"); return 0; }
                result.ThrowOnError();
                return value;
            }
            return 0;
        }

        public void SetX(int index, ulong value)
        {
            if (index == 31)
            {
                var result = HvApi.hv_vcpu_set_sys_reg(_vcpu, HvSysReg.SP_EL0, value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, "HV_BAD_ARGUMENT on SP_EL0 (set)"); return; }
                result.ThrowOnError();
            }
            else if (index >= 0 && index <= 30)
            {
                var result = HvApi.hv_vcpu_set_reg(_vcpu, HvReg.X0 + (uint)index, value);
                if (result == HvResult.BadArgument) { Logger.Warning?.Print(LogClass.Cpu, $"HV_BAD_ARGUMENT on X{index} (set)"); return; }
                result.ThrowOnError();
            }
        }

        public V128 GetV(int index)
        {
            if (index < 0 || index > 31)
                return V128.Zero;

            var result = HvApi.hv_vcpu_get_simd_fp_reg(_vcpu, HvSimdFPReg.Q0 + (uint)index, out HvSimdFPUchar16 value);
            if (result == HvResult.BadArgument)
            {
                Logger.Warning?.Print(LogClass.Cpu, $"HV_BAD_ARGUMENT on V{index} (SIMD)");
                return V128.Zero;
            }
            if (result != HvResult.Success)
                result.ThrowOnError();

            return new V128(value.Low, value.High);
        }

        public void SetV(int index, V128 value)
        {
            if (index < 0 || index > 31)
                return;

            var result = _setSimdFpReg(_vcpu, HvSimdFPReg.Q0 + (uint)index, value, _setSimdFpRegNativePtr);
            if (result == HvResult.BadArgument)
            {
                Logger.Warning?.Print(LogClass.Cpu, $"HV_BAD_ARGUMENT on V{index} (SIMD set)");
                return;
            }
            if (result != HvResult.Success)
                result.ThrowOnError();
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
