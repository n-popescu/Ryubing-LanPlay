using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Operations
{
    sealed class OpMovFactory : IOperationFactory
    {
        private OpMovFactory() { }

        public static IOperation CreateFor<T>(IOperand destination, IOperand lhs, IOperand rhs) where T : unmanaged, IBinaryInteger<T>
            => new OpMov<T>(destination, lhs);
    }
    class OpMov<T> : IOperation where T : unmanaged, INumber<T>
    {
        readonly IOperand _destination;
        readonly IOperand _source;

        public OpMov(IOperand destination, IOperand source)
        {
            _destination = destination;
            _source = source;
        }

        public void Execute()
        {
            _destination.Set(_source.Get<T>());
        }
    }
}
