using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Operations
{
    sealed class OpAndFactory : IOperationFactory
    {
        private OpAndFactory() { }

        public static IOperation CreateFor<T>(IOperand destination, IOperand lhs, IOperand rhs) where T : unmanaged, IBinaryInteger<T>
            => new OpAnd<T>(destination, lhs, rhs);
    }
    class OpAnd<T> : IOperation where T : unmanaged, IBinaryNumber<T>
    {
        readonly IOperand _destination;
        readonly IOperand _lhs;
        readonly IOperand _rhs;

        public OpAnd(IOperand destination, IOperand lhs, IOperand rhs)
        {
            _destination = destination;
            _lhs = lhs;
            _rhs = rhs;
        }

        public void Execute()
        {
            _destination.Set(_lhs.Get<T>() & _rhs.Get<T>());
        }
    }
}
