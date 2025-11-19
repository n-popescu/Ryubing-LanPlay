using Ryujinx.HLE.HOS.Tamper.Operations;
using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Conditions
{
    interface IConditionFactory
    {
        static abstract ICondition CreateFor<T>(IOperand lhs, IOperand rhs) where T : unmanaged, INumber<T>;
    }
}
