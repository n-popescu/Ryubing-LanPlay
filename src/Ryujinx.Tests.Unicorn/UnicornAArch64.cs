using System;
using UnicornEngine.Const;

namespace Ryujinx.Tests.Unicorn
{
    public class UnicornAArch64 : IDisposable
    {
        private readonly UnicornEngine.Unicorn _uc;
        private bool _isDisposed;

        public IndexedProperty<int, ulong> X => new(GetX, SetX);

        public IndexedProperty<int, SimdValue> Q => new(GetQ, SetQ);

        public ulong LR
        {
            get => GetRegister(RegArm64.Lr);
            set => SetRegister(RegArm64.Lr, value);
        }

        public ulong SP
        {
            get => GetRegister(RegArm64.Sp);
            set => SetRegister(RegArm64.Sp, value);
        }

        public ulong PC
        {
            get => GetRegister(RegArm64.Pc);
            set => SetRegister(RegArm64.Pc, value);
        }

        public uint Pstate
        {
            get => (uint)GetRegister(RegArm64.Pstate);
            set => SetRegister(RegArm64.Pstate, value);
        }

        public int Fpcr
        {
            get => (int)GetRegister(RegArm64.Fpcr);
            set => SetRegister(RegArm64.Fpcr, (uint)value);
        }

        public int Fpsr
        {
            get => (int)GetRegister(RegArm64.Fpsr);
            set => SetRegister(RegArm64.Fpsr, (uint)value);
        }

        public bool OverflowFlag
        {
            get => (Pstate & 0x10000000u) != 0;
            set => Pstate = (Pstate & ~0x10000000u) | (value ? 0x10000000u : 0u);
        }

        public bool CarryFlag
        {
            get => (Pstate & 0x20000000u) != 0;
            set => Pstate = (Pstate & ~0x20000000u) | (value ? 0x20000000u : 0u);
        }

        public bool ZeroFlag
        {
            get => (Pstate & 0x40000000u) != 0;
            set => Pstate = (Pstate & ~0x40000000u) | (value ? 0x40000000u : 0u);
        }

        public bool NegativeFlag
        {
            get => (Pstate & 0x80000000u) != 0;
            set => Pstate = (Pstate & ~0x80000000u) | (value ? 0x80000000u : 0u);
        }

        public UnicornAArch64()
        {
            _uc = new UnicornEngine.Unicorn();
            _uc.Open(UcArch.Arm64, UcMode.LittleEndian);
            _uc.SetCpuModel(CpuArm64.Arm64A57);

            SetRegister(RegArm64.CpacrEl1, 0x00300000);
        }

        ~UnicornAArch64()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _uc.Close();
                _isDisposed = true;
            }
        }

        public void RunForCount(ulong count)
        {
            // FIXME: untilAddr should be 0xFFFFFFFFFFFFFFFFul
            _uc.StartEmulation((nint)this.PC, -1, 0, count);
        }

        public void Step()
        {
            RunForCount(1);
        }

        private static readonly RegArm64[] _xRegisters =
        [
            RegArm64.X0,
            RegArm64.X1,
            RegArm64.X2,
            RegArm64.X3,
            RegArm64.X4,
            RegArm64.X5,
            RegArm64.X6,
            RegArm64.X7,
            RegArm64.X8,
            RegArm64.X9,
            RegArm64.X10,
            RegArm64.X11,
            RegArm64.X12,
            RegArm64.X13,
            RegArm64.X14,
            RegArm64.X15,
            RegArm64.X16,
            RegArm64.X17,
            RegArm64.X18,
            RegArm64.X19,
            RegArm64.X20,
            RegArm64.X21,
            RegArm64.X22,
            RegArm64.X23,
            RegArm64.X24,
            RegArm64.X25,
            RegArm64.X26,
            RegArm64.X27,
            RegArm64.X28,
            RegArm64.X29,
            RegArm64.X30
        ];

        private static readonly RegArm64[] _qRegisters =
        [
            RegArm64.Q0,
            RegArm64.Q1,
            RegArm64.Q2,
            RegArm64.Q3,
            RegArm64.Q4,
            RegArm64.Q5,
            RegArm64.Q6,
            RegArm64.Q7,
            RegArm64.Q8,
            RegArm64.Q9,
            RegArm64.Q10,
            RegArm64.Q11,
            RegArm64.Q12,
            RegArm64.Q13,
            RegArm64.Q14,
            RegArm64.Q15,
            RegArm64.Q16,
            RegArm64.Q17,
            RegArm64.Q18,
            RegArm64.Q19,
            RegArm64.Q20,
            RegArm64.Q21,
            RegArm64.Q22,
            RegArm64.Q23,
            RegArm64.Q24,
            RegArm64.Q25,
            RegArm64.Q26,
            RegArm64.Q27,
            RegArm64.Q28,
            RegArm64.Q29,
            RegArm64.Q30,
            RegArm64.Q31
        ];

        public ulong GetX(int index)
        {
            if ((uint)index > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetRegister(_xRegisters[index]);
        }

        public void SetX(int index, ulong value)
        {
            if ((uint)index > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            SetRegister(_xRegisters[index], value);
        }

        public SimdValue GetQ(int index)
        {
            if ((uint)index > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetVector(_qRegisters[index]);
        }

        public void SetQ(int index, SimdValue value)
        {
            if ((uint)index > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            SetVector(_qRegisters[index], value);
        }

        private ulong GetRegister(RegArm64 register)
        {
            byte[] data = new byte[8];

            _uc.RegRead(register, data);

            return BitConverter.ToUInt64(data);
        }

        private void SetRegister(RegArm64 register, ulong value)
        {
            byte[] data = BitConverter.GetBytes(value);

            _uc.RegWrite(register, data);
        }

        private SimdValue GetVector(RegArm64 register)
        {
            byte[] data = new byte[16];

            _uc.RegRead(register, data);

            return new SimdValue(data);
        }

        private void SetVector(RegArm64 register, SimdValue value)
        {
            byte[] data = value.ToArray();

            _uc.RegWrite(register, data);
        }

        public byte[] MemoryRead(ulong address, ulong size)
        {
            _uc.MemRead((nint)address, size, out Span<byte> value);

            return value.ToArray();
        }

        public byte MemoryRead8(ulong address) => MemoryRead(address, 1)[0];
        public ushort MemoryRead16(ulong address) => BitConverter.ToUInt16(MemoryRead(address, 2), 0);
        public uint MemoryRead32(ulong address) => BitConverter.ToUInt32(MemoryRead(address, 4), 0);
        public ulong MemoryRead64(ulong address) => BitConverter.ToUInt64(MemoryRead(address, 8), 0);

        public void MemoryWrite(ulong address, byte[] value)
        {
            _uc.MemWrite((nint)address, value);
        }

        public void MemoryWrite8(ulong address, byte value) => MemoryWrite(address, [value]);
        public void MemoryWrite16(ulong address, short value) => MemoryWrite(address, BitConverter.GetBytes(value));
        public void MemoryWrite16(ulong address, ushort value) => MemoryWrite(address, BitConverter.GetBytes(value));
        public void MemoryWrite32(ulong address, int value) => MemoryWrite(address, BitConverter.GetBytes(value));
        public void MemoryWrite32(ulong address, uint value) => MemoryWrite(address, BitConverter.GetBytes(value));
        public void MemoryWrite64(ulong address, long value) => MemoryWrite(address, BitConverter.GetBytes(value));
        public void MemoryWrite64(ulong address, ulong value) => MemoryWrite(address, BitConverter.GetBytes(value));

        public void MemoryMap(ulong address, ulong size, UcProtection permissions)
        {
            _uc.MemMap((nint)address, size, permissions);
        }

        public void MemoryUnmap(ulong address, ulong size)
        {
            _uc.MemUnmap((nint)address, size);
        }

        public void MemoryProtect(ulong address, ulong size, UcProtection permissions)
        {
            _uc.MemProtect((nint)address, size, permissions);
        }
    }
}
