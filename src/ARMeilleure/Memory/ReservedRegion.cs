using System;

namespace ARMeilleure.Memory
{
    public class ReservedRegion : IDisposable
    {
        public const int DefaultGranularity = 65536; // Mapping granularity in Windows.

        public IJitMemoryBlock Block { get; }
        public IJitMemoryAllocator Allocator { get; }

        public nint Pointer => Block.Pointer;

        private readonly ulong _maxSize;
        private readonly ulong _sizeGranularity;
        public ulong CurrentSize { get; private set; }

        public ReservedRegion(IJitMemoryAllocator allocator, ulong maxSize, ulong granularity = 0)
        {
            if (granularity == 0)
            {
                granularity = DefaultGranularity;
            }

            Allocator = allocator;
            Block = allocator.Reserve(maxSize);
            _maxSize = maxSize;
            _sizeGranularity = granularity;
            CurrentSize = 0;
        }

        public void ExpandIfNeeded(ulong desiredSize)
        {
            if (desiredSize > _maxSize)
            {
                throw new OutOfMemoryException();
            }

            if (desiredSize > CurrentSize)
            {
                // Lock, and then check again. We only want to commit once.
                lock (this)
                {
                    if (desiredSize >= CurrentSize)
                    {
                        ulong overflowBytes = desiredSize - CurrentSize;
                        ulong moreToCommit = (((_sizeGranularity - 1) + overflowBytes) / _sizeGranularity) * _sizeGranularity; // Round up.
                        Block.Commit(CurrentSize, moreToCommit);
                        CurrentSize += moreToCommit;
                    }
                }
            }
        }

        public void Dispose()
        {
            Block.Dispose();
        }
    }
}
