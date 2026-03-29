using Ryujinx.HLE.HOS.Tamper.Operations;
using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Conditions
{
    sealed class CondNEFactory : IConditionFactory
    {
        private CondNEFactory() { }

        public static ICondition CreateFor<T>(IOperand lhs, IOperand rhs) where T : unmanaged, INumber<T>
            => new CondNE<T>(lhs, rhs);
    }
    class CondNE<T> : ICondition where T : unmanaged, INumber<T>
    {
        private readonly IOperand _lhs;
        private readonly IOperand _rhs;

        public CondNE(IOperand lhs, IOperand rhs)
        {
            _lhs = lhs;
            _rhs = rhs;
        }

        public bool Evaluate()
        {
            return _lhs.Get<T>() != _rhs.Get<T>();
        }

        public static ICondition CreateFor<T1>(IOperand lhs, IOperand rhs) where T1 : INumber<T1>
            => new CondNE<T>(lhs, rhs);
    }
}
