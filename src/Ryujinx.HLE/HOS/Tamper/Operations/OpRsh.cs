using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Operations
{
    sealed class OpRshFactory : IOperationFactory
    {
        private OpRshFactory() { }

        public static IOperation CreateFor<T>(IOperand destination, IOperand lhs, IOperand rhs) where T : unmanaged, IBinaryInteger<T>
            => new OpRsh<T>(destination, lhs, rhs);
    }
    class OpRsh<T> : IOperation where T : unmanaged, IBinaryInteger<T>
    {
        readonly IOperand _destination;
        readonly IOperand _lhs;
        readonly IOperand _rhs;

        public OpRsh(IOperand destination, IOperand lhs, IOperand rhs)
        {
            _destination = destination;
            _lhs = lhs;
            _rhs = rhs;
        }

        public void Execute()
        {
            _destination.Set(_lhs.Get<T>() >> int.CreateTruncating(_rhs.Get<T>()));
        }
    }
}
