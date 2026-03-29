using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Operations
{
    sealed class OpAddFactory : IOperationFactory
    {
        private OpAddFactory() { }

        public static IOperation CreateFor<T>(IOperand destination, IOperand lhs, IOperand rhs) where T : unmanaged, IBinaryInteger<T>
            => new OpAdd<T>(destination, lhs, rhs);
    }
    class OpAdd<T> : IOperation where T : unmanaged, INumber<T>
    {
        readonly IOperand _destination;
        readonly IOperand _lhs;
        readonly IOperand _rhs;

        public OpAdd(IOperand destination, IOperand lhs, IOperand rhs)
        {
            _destination = destination;
            _lhs = lhs;
            _rhs = rhs;
        }

        public void Execute()
        {
            _destination.Set(_lhs.Get<T>() + _rhs.Get<T>());
        }
    }
}
