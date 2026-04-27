using ARMeilleure.Memory;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Ryujinx.Cpu.AppleHv
{
    [SupportedOSPlatform("macos")]
    internal sealed class HvCpuContext : ICpuContext
    {
        private readonly ITickSource _tickSource;
        private readonly HvMemoryManager _memoryManager;

        public HvCpuContext(ITickSource tickSource, IMemoryManager memory, bool for64Bit)
        {
            _tickSource = tickSource;
            _memoryManager = (HvMemoryManager)memory;
            _ = for64Bit;
        }

        /// <inheritdoc/>
        public IExecutionContext CreateExecutionContext(ExceptionCallbacks exceptionCallbacks)
        {
            return new HvExecutionContext(_tickSource, exceptionCallbacks);
        }

        /// <inheritdoc/>
        public void Execute(IExecutionContext context, ulong address)
        {
            ((HvExecutionContext)context).Execute(_memoryManager, address);
        }

        /// <inheritdoc/>
        public void InvalidateCacheRegion(ulong address, ulong size)
        {
        }

        public IDiskCacheLoadState LoadDiskCache(
            string titleIdText,
            string displayVersion,
            bool enabled,
            string stockDisplayVersion = null,
            IReadOnlyList<(ulong Start, ulong Size)> moddedAddressRanges = null,
            bool enableStockProfileSidecarMining = false)
        {
            return new DummyDiskCacheLoadState();
        }

        public void PrepareCodeRange(ulong address, ulong size)
        {
        }

        /// <inheritdoc/>
        public void RegisterNroModule(byte[] buildId, ulong address, ulong size, byte[] fileImage)
        {
        }

        /// <inheritdoc/>
        public void UnregisterNroModule(ulong address)
        {
        }

        public void Dispose()
        {
        }
    }
}
