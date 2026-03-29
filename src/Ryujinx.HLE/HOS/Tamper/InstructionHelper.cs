using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Tamper.Conditions;
using Ryujinx.HLE.HOS.Tamper.Operations;
using System;
using System.Globalization;

namespace Ryujinx.HLE.HOS.Tamper
{
    class InstructionHelper
    {
        private const int CodeTypeIndex = 0;

        public static void Emit(IOperation operation, CompilationContext context)
        {
            context.CurrentOperations.Add(operation);
        }

        public static void Emit<TOp>(byte width, CompilationContext context, IOperand destination, IOperand lhs, IOperand rhs) where TOp : IOperationFactory
        {
            Emit(Create<TOp>(width, destination, lhs, rhs), context);
        }

        public static ICondition CreateCondition(Comparison comparison, byte width, IOperand lhs, IOperand rhs)
        {
            ICondition CreateCore<TOp>() where TOp : IConditionFactory
            {
                return width switch
                {
                    1 => TOp.CreateFor<byte>(lhs, rhs),
                    2 => TOp.CreateFor<ushort>(lhs, rhs),
                    4 => TOp.CreateFor<uint>(lhs, rhs),
                    8 => TOp.CreateFor<ulong>(lhs, rhs),
                    _ => throw new NotSupportedException(),
                };
            }

            return comparison switch
            {
                Comparison.Greater => CreateCore<CondGTFactory>(),
                Comparison.GreaterOrEqual => CreateCore<CondGEFactory>(),
                Comparison.Less => CreateCore<CondLTFactory>(),
                Comparison.LessOrEqual => CreateCore<CondLEFactory>(),
                Comparison.Equal => CreateCore<CondEQFactory>(),
                Comparison.NotEqual => CreateCore<CondNEFactory>(),
                _ => throw new TamperCompilationException($"Invalid comparison {comparison} in Atmosphere cheat"),
            };
        }

        public static IOperation Create<TOp>(byte width, IOperand destination, IOperand lhs, IOperand rhs) where TOp : IOperationFactory
        {
            return width switch
            {
                1 => TOp.CreateFor<byte>(destination, lhs, rhs),
                2 => TOp.CreateFor<ushort>(destination, lhs, rhs),
                4 => TOp.CreateFor<uint>(destination, lhs, rhs),
                8 => TOp.CreateFor<ulong>(destination, lhs, rhs),
                _ => throw new NotSupportedException(),
            };
        }

        public static ulong GetImmediate(byte[] instruction, int index, int nybbleCount)
        {
            ulong value = 0;

            for (int i = 0; i < nybbleCount; i++)
            {
                value <<= 4;
                value |= instruction[index + i];
            }

            return value;
        }

        public static CodeType GetCodeType(byte[] instruction)
        {
            int codeType = instruction[CodeTypeIndex];

            if (codeType >= 0xC)
            {
                byte extension = instruction[CodeTypeIndex + 1];
                codeType = (codeType << 4) | extension;

                if (extension == 0xF)
                {
                    extension = instruction[CodeTypeIndex + 2];
                    codeType = (codeType << 4) | extension;
                }
            }

            return (CodeType)codeType;
        }

        public static byte[] ParseRawInstruction(string rawInstruction)
        {
            const int WordSize = 2 * sizeof(uint);

            // Instructions are multi-word, with 32bit words. Split the raw instruction
            // and parse each word into individual nybbles of bits.

            string[] words = rawInstruction.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            byte[] instruction = new byte[WordSize * words.Length];

            if (words.Length == 0)
            {
                throw new TamperCompilationException("Empty instruction in Atmosphere cheat");
            }

            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                string word = words[wordIndex];

                if (word.Length != WordSize)
                {
                    throw new TamperCompilationException($"Invalid word length for {word} in Atmosphere cheat");
                }

                for (int nybbleIndex = 0; nybbleIndex < WordSize; nybbleIndex++)
                {
                    int index = wordIndex * WordSize + nybbleIndex;

                    instruction[index] = byte.Parse(word.AsSpan(nybbleIndex, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                }
            }

            return instruction;
        }
    }
}
