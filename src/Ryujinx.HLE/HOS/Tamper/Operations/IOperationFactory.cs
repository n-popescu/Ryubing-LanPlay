using System;
using System.Numerics;

namespace Ryujinx.HLE.HOS.Tamper.Operations
{
    interface IOperationFactory
    {
        static abstract IOperation CreateFor<T>(IOperand destination, IOperand lhs, IOperand rhs) where T : unmanaged, IBinaryInteger<T>;
    }
}
