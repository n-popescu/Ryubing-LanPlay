using Ryujinx.HLE.HOS.Tamper.Operations;
using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper
{
    class Value<TP> : IOperand where TP : unmanaged, INumber<TP>
    {
        private TP _value;

        public Value(TP value)
        {
            _value = value;
        }

        public T Get<T>() where T : unmanaged, INumber<T>
        {
            return T.CreateTruncating(_value);
        }

        public void Set<T>(T value) where T : unmanaged, INumber<T>
        {
            _value = TP.CreateTruncating(value);
        }
    }
}
