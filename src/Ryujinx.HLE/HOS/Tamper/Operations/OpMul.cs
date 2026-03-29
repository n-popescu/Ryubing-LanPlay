using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Operations
{
    sealed class OpMulFactory : IOperationFactory
    {
        private OpMulFactory() { }

        public static IOperation CreateFor<T>(IOperand destination, IOperand lhs, IOperand rhs) where T : unmanaged, IBinaryInteger<T>
            => new OpMul<T>(destination, lhs, rhs);
    }
    class OpMul<T> : IOperation where T : unmanaged, INumber<T>
    {
        readonly IOperand _destination;
        readonly IOperand _lhs;
        readonly IOperand _rhs;

        public OpMul(IOperand destination, IOperand lhs, IOperand rhs)
        {
            _destination = destination;
            _lhs = lhs;
            _rhs = rhs;
        }

        public void Execute()
        {
            _destination.Set(_lhs.Get<T>() * _rhs.Get<T>());
        }
    }
}
