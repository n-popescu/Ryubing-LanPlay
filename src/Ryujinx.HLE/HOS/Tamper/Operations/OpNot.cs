using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Operations
{
    sealed class OpNotFactory : IOperationFactory
    {
        private OpNotFactory() { }

        public static IOperation CreateFor<T>(IOperand destination, IOperand lhs, IOperand rhs) where T : unmanaged, IBinaryInteger<T>
            => new OpNot<T>(destination, lhs);
    }
    class OpNot<T> : IOperation where T : unmanaged, IBinaryNumber<T>
    {
        readonly IOperand _destination;
        readonly IOperand _source;

        public OpNot(IOperand destination, IOperand source)
        {
            _destination = destination;
            _source = source;
        }

        public void Execute()
        {
            _destination.Set(~_source.Get<T>());
        }
    }
}
