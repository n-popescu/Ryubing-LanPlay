using Ryujinx.HLE.HOS.Tamper.Operations;
using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Conditions
{
    sealed class CondEQFactory : IConditionFactory
    {
        private CondEQFactory() { }

        public static ICondition CreateFor<T>(IOperand lhs, IOperand rhs) where T : unmanaged, INumber<T>
            => new CondEQ<T>(lhs, rhs);
    }
    class CondEQ<T> : ICondition where T : unmanaged, INumber<T>
    {
        private readonly IOperand _lhs;
        private readonly IOperand _rhs;

        public CondEQ(IOperand lhs, IOperand rhs)
        {
            _lhs = lhs;
            _rhs = rhs;
        }

        public bool Evaluate()
        {
            return _lhs.Get<T>() == _rhs.Get<T>();
        }
    }
}
