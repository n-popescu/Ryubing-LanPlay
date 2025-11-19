using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Operations
{
    interface IOperand
    {
        public T Get<T>() where T : unmanaged, INumber<T>;
        public void Set<T>(T value) where T : unmanaged, INumber<T>;
    }
}
