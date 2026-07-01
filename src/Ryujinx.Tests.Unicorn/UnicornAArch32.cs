using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnicornEngine;
using UnicornEngine.Const;

namespace Ryujinx.Tests.Unicorn
{
    public class UnicornAArch32 : IDisposable
    {
        private readonly UnicornEngine.Unicorn _uc;
        private bool _isDisposed;

        public IndexedProperty<int, uint> R => new(GetX, SetX);

        public IndexedProperty<int, SimdValue> Q => new(GetQ, SetQ);

        public uint LR
        {
            get => GetRegister(RegArm.Lr);
            set => SetRegister(RegArm.Lr, value);
        }

        public uint SP
        {
            get => GetRegister(RegArm.Sp);
            set => SetRegister(RegArm.Sp, value);
        }

        public uint PC
        {
            get => GetRegister(RegArm.Pc) & 0xfffffffeu;
            set => SetRegister(RegArm.Pc, (value & 0xfffffffeu) | (ThumbFlag ? 1u : 0u));
        }

        public uint CPSR
        {
            get => GetRegister(RegArm.Cpsr);
            set => SetRegister(RegArm.Cpsr, value);
        }

        public int Fpscr
        {
            get => (int)GetRegister(RegArm.Fpscr);
            set => SetRegister(RegArm.Fpscr, (uint)value);
        }

        public bool QFlag
        {
            get => (CPSR & 0x8000000u) != 0;
            set => CPSR = (CPSR & ~0x8000000u) | (value ? 0x8000000u : 0u);
        }

        public bool OverflowFlag
        {
            get => (CPSR & 0x10000000u) != 0;
            set => CPSR = (CPSR & ~0x10000000u) | (value ? 0x10000000u : 0u);
        }

        public bool CarryFlag
        {
            get => (CPSR & 0x20000000u) != 0;
            set => CPSR = (CPSR & ~0x20000000u) | (value ? 0x20000000u : 0u);
        }

        public bool ZeroFlag
        {
            get => (CPSR & 0x40000000u) != 0;
            set => CPSR = (CPSR & ~0x40000000u) | (value ? 0x40000000u : 0u);
        }

        public bool NegativeFlag
        {
            get => (CPSR & 0x80000000u) != 0;
            set => CPSR = (CPSR & ~0x80000000u) | (value ? 0x80000000u : 0u);
        }

        public bool ThumbFlag
        {
            get => (CPSR & 0x00000020u) != 0;
            set
            {
                CPSR = (CPSR & ~0x00000020u) | (value ? 0x00000020u : 0u);
                SetRegister(RegArm.Pc, (GetRegister(RegArm.Pc) & 0xfffffffeu) | (value ? 1u : 0u));
            }
        }

        public UnicornAArch32()
        {
            _uc = new UnicornEngine.Unicorn();
            _uc.Open(UcArch.Arm, UcMode.LittleEndian);

            ArmCpRegister reg = new()
            {
                Cp = 15,
                Is64Bit = 0,
                Sec = 0,
                Crn = 13,
                Crm = 0,
                Opc1 = 0,
                Opc2 = 2,
                Val = 0
            };
            
            _uc.CpRegRead(ref reg);
            reg.Val |= 0xf00000;
            _uc.CpRegWrite(reg);
            
            SetRegister(RegArm.Fpexc, 0x40000000);
        }

        ~UnicornAArch32()
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
            // FIXME: untilAddr should be 0xFFFFFFFFFFFFFFFFu
            _uc.StartEmulation((nint)this.PC, -1, 0, count);
        }

        public void Step()
        {
            RunForCount(1);
        }

        public void SetExits(nint[] exits)
        {
            _uc.EnableExits();

            _uc.SetExits(exits);
        }

        private static readonly RegArm[] _xRegisters =
        [
            RegArm.R0,
            RegArm.R1,
            RegArm.R2,
            RegArm.R3,
            RegArm.R4,
            RegArm.R5,
            RegArm.R6,
            RegArm.R7,
            RegArm.R8,
            RegArm.R9,
            RegArm.R10,
            RegArm.R11,
            RegArm.R12,
            RegArm.R13,
            RegArm.R14,
            RegArm.R15
        ];

#pragma warning disable IDE0051, IDE0052 // Remove unused private member
        private static readonly RegArm[] _qRegisters =
        [
            RegArm.Q0,
            RegArm.Q1,
            RegArm.Q2,
            RegArm.Q3,
            RegArm.Q4,
            RegArm.Q5,
            RegArm.Q6,
            RegArm.Q7,
            RegArm.Q8,
            RegArm.Q9,
            RegArm.Q10,
            RegArm.Q11,
            RegArm.Q12,
            RegArm.Q13,
            RegArm.Q14,
            RegArm.Q15
        ];
#pragma warning restore IDE0051, IDE0052

        public uint GetX(int index)
        {
            if ((uint)index > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetRegister(_xRegisters[index]);
        }

        public void SetX(int index, uint value)
        {
            if ((uint)index > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            SetRegister(_xRegisters[index], value);
        }

        public SimdValue GetQ(int index)
        {
            if ((uint)index > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            // Getting quadword registers from Unicorn A32 seems to be broken, so we combine its 2 doubleword registers instead.
            return GetVector(RegArm.D0 + index * 2);
        }

        public void SetQ(int index, SimdValue value)
        {
            if ((uint)index > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            SetVector(RegArm.D0 + index * 2, value);
        }

        public uint GetRegister(RegArm register)
        {
            Span<uint> data = [0];

            _uc.RegRead(register, MemoryMarshal.AsBytes(data));

            return data[0];
        }

        public void SetRegister(RegArm register, uint value)
        {
            Span<uint> data = [value];

            _uc.RegWrite(register, MemoryMarshal.AsBytes(data));
        }

        public SimdValue GetVector(RegArm register)
        {
            Span<SimdValue> data = new SimdValue[1];

            _uc.RegRead(register, MemoryMarshal.AsBytes(data)[..7]);
            _uc.RegRead(register + 1, MemoryMarshal.AsBytes(data)[8..15]);

            return data[0];
        }

        private void SetVector(RegArm register, SimdValue value)
        {
            Span<SimdValue> data = [value];
            
            _uc.RegWrite(register, MemoryMarshal.AsBytes(data)[..7]);
            _uc.RegWrite(register + 1, MemoryMarshal.AsBytes(data)[8..15]);
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
