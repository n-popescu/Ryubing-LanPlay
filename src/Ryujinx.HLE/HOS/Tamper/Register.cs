using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Tamper.Operations;
using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper
{
    class Register : IOperand
    {
        private ulong _register = 0;
        private readonly string _alias;

        public Register(string alias)
        {
            _alias = alias;
        }

        public T Get<T>() where T : unmanaged, INumber<T>
        {
            return T.CreateTruncating(_register);
        }

        public void Set<T>(T value) where T : unmanaged, INumber<T>
        {
            Logger.Debug?.Print(LogClass.TamperMachine, $"{_alias}: {value}");

            _register = ulong.CreateTruncating(value);
        }
    }
}
