using ARMeilleure.Memory;
using Ryujinx.Common.Logging;
using Ryujinx.Memory;
using Ryujinx.Memory.Range;
using Ryujinx.Memory.Tracking;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Cpu.Jit
{
    /// <summary>
    /// Represents a CPU memory manager.
    /// </summary>
    public sealed class MemoryManager : VirtualMemoryManagerRefCountedBase, IMemoryManager, IVirtualMemoryManagerTracked
    {
        private const int PteSize = 8;
        private const int PointerTagBit = 62;
        private readonly MemoryBlock _backingMemory;
        private readonly InvalidAccessHandler _invalidAccessHandler;

        private long _invalidAccessCount = 0;
        public bool UsesPrivateAllocations => false;
        public int AddressSpaceBits { get; }
        public nint PageTablePointer => (nint)_pageTable.Pointer;
        private readonly MemoryBlock _pageTable;
        private readonly ManagedPageFlags _pages;

        public MemoryManagerType Type => MemoryManagerType.SoftwarePageTable;
        public MemoryTracking Tracking { get; }
        public event Action<ulong, ulong> UnmapEvent;

        protected override ulong AddressSpaceSize { get; }

        public MemoryManager(MemoryBlock backingMemory, ulong addressSpaceSize, InvalidAccessHandler invalidAccessHandler = null)
        {
            _backingMemory = backingMemory;
            _invalidAccessHandler = invalidAccessHandler;

            ulong asSize = PageSize;
            int asBits = PageBits;
            while (asSize < addressSpaceSize)
            {
                asSize <<= 1;
                asBits++;
            }
            AddressSpaceBits = asBits;
            AddressSpaceSize = asSize;

            _pageTable = new MemoryBlock((asSize / PageSize) * PteSize);
            _pages = new ManagedPageFlags(AddressSpaceBits);
            Tracking = new MemoryTracking(this, PageSize);
        }

        private static bool IsPoisoned(ulong va)
        {
            if (va == 0 || va == 1) return true;
            if ((va & 0x6969696969696969UL) != 0) return true;
            if ((va & 0x00F0F0F0F0F0F0F0UL) == 0x0034b4b000000000UL) return true;
            if ((va & 0xFFFFFFFF00000000UL) == 0x0034b4b900000000UL) return true;
            if (va < 0x10000) return true;
            return false;
        }

        /// <inheritdoc/>
        public void Map(ulong va, ulong pa, ulong size, MemoryMapFlags flags)
        {
            AssertValidAddressAndSize(va, size);

            ulong remainingSize = size;
            ulong oVa = va;
            while (remainingSize != 0)
            {
                _pageTable.Write((va / PageSize) * PteSize, PaToPte(pa));

                va += PageSize;
                pa += PageSize;
                remainingSize -= PageSize;
            }

            _pages.AddMapping(oVa, size);
            Tracking.Map(oVa, size);
        }

        /// <inheritdoc/>
        public void Unmap(ulong va, ulong size)
        {
            // If size is 0, there's nothing to unmap, just exit early.
            if (size == 0)
            {
                return;
            }

            AssertValidAddressAndSize(va, size);

            UnmapEvent?.Invoke(va, size);
            Tracking.Unmap(va, size);
            _pages.RemoveMapping(va, size);

            ulong remainingSize = size;
            while (remainingSize != 0)
            {
                _pageTable.Write((va / PageSize) * PteSize, 0UL);

                va += PageSize;
                remainingSize -= PageSize;
            }
        }

        public override T ReadTracked<T>(ulong va)
        {
            try
            {
                return base.ReadTracked<T>(va);
            }
            catch (InvalidMemoryRegionException)
            {
                if (_invalidAccessHandler == null || !_invalidAccessHandler(va))
                {
                    throw;
                }

                return default;
            }
        }

        /// <inheritdoc/>
        public T ReadGuest<T>(ulong va) where T : unmanaged
        {
            try
            {
                return Read<T>(va);
            }
            catch
            {
                if (IsPoisonedPointer(va))
                {
                    if (Interlocked.Increment(ref _invalidAccessCount) % 256 == 0)
                    {
                        Ryujinx.Common.Logging.Logger.Warning?.Print(Ryujinx.Common.Logging.LogClass.Cpu, 
                            $"Suppressed exception on poisoned address 0x{va:X16}");
                    }
                    return default;
                }
                throw;
            }
        }

        public override T Read<T>(ulong va)
        {
            if (IsPoisoned(va))
            {
                LogSuppressed(va, "Read");
                return default;
            }
            return base.Read<T>(va);
        }



        /// <inheritdoc/>
        public override void Read(ulong va, Span<byte> data)
        {
            if (IsPoisoned(va))
            {
                LogSuppressed(va, "ReadSpan");
                data.Clear();
                return;
            }
            base.Read(va, data);
        }
        public override void Write(ulong va, ReadOnlySpan<byte> data)
        {
            if (IsPoisoned(va))
            {
                LogSuppressed(va, "Write");
                return;
            }
            base.Write(va, data);
        }

        private void LogSuppressed(ulong va, string op)
        {
            if (Interlocked.Increment(ref _invalidAccessCount) % 64 == 0)
            {
                Ryujinx.Common.Logging.Logger.Warning?.Print(Ryujinx.Common.Logging.LogClass.Cpu, 
                    $"Suppressed {op} @ 0x{va:X16}");
            }
        }

        /// <inheritdoc/>
        public void WriteGuest<T>(ulong va, T value) where T : unmanaged
        {
            Span<byte> data = MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref value, 1));

            SignalMemoryTrackingImpl(va, (ulong)data.Length, true, true);

            Write(va, data);
        }

        public override void WriteUntracked(ulong va, ReadOnlySpan<byte> data)
        {
            try
            {
                base.WriteUntracked(va, data);
            }
            catch (InvalidMemoryRegionException)
            {
                if (_invalidAccessHandler == null || !_invalidAccessHandler(va))
                {
                    throw;
                }
            }
        }

        public override ReadOnlySequence<byte> GetReadOnlySequence(ulong va, int size, bool tracked = false)
        {
            try
            {
                return base.GetReadOnlySequence(va, size, tracked);
            }
            catch (InvalidMemoryRegionException)
            {
                if (_invalidAccessHandler == null || !_invalidAccessHandler(va))
                {
                    throw;
                }

                return ReadOnlySequence<byte>.Empty;
            }
        }

        public ref T GetRef<T>(ulong va) where T : unmanaged
        {
            if (!IsContiguous(va, Unsafe.SizeOf<T>()))
            {
                ThrowMemoryNotContiguous();
            }

            SignalMemoryTracking(va, (ulong)Unsafe.SizeOf<T>(), true);

            return ref _backingMemory.GetRef<T>(GetPhysicalAddressInternal(va));
        }

        /// <inheritdoc/>
        public IEnumerable<HostMemoryRange> GetHostRegions(ulong va, ulong size)
        {
            if (size == 0)
            {
                yield break;
            }

            IEnumerable<MemoryRange> guestRegions = GetPhysicalRegionsImpl(va, size);
            if (guestRegions == null)
            {
                yield break;
            }

            foreach (MemoryRange guestRegion in guestRegions)
            {
                nint pointer = _backingMemory.GetPointer(guestRegion.Address, guestRegion.Size);
                yield return new HostMemoryRange((nuint)(ulong)pointer, guestRegion.Size);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<MemoryRange> GetPhysicalRegions(ulong va, ulong size)
        {
            if (size == 0)
            {
                yield break;
            }

            foreach (MemoryRange physicalRegion in GetPhysicalRegionsImpl(va, size))
            {
                yield return physicalRegion;
            }
        }

        private IEnumerable<MemoryRange> GetPhysicalRegionsImpl(ulong va, ulong size)
        {
            if (!ValidateAddress(va) || !ValidateAddressAndSize(va, size))
            {
                yield break;
            }

            int pages = GetPagesCount(va, (uint)size, out va);

            ulong regionStart = GetPhysicalAddressInternal(va);
            ulong regionSize = PageSize;

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize))
                {
                    yield break;
                }

                ulong newPa = GetPhysicalAddressInternal(va + PageSize);

                if (GetPhysicalAddressInternal(va) + PageSize != newPa)
                {
                    yield return new MemoryRange(regionStart, regionSize);
                    regionStart = newPa;
                    regionSize = 0;
                }

                va += PageSize;
                regionSize += PageSize;
            }

            yield return new MemoryRange(regionStart, regionSize);
        }

        /// <inheritdoc/>
        public bool IsRangeMapped(ulong va, ulong size)
        {
            if (size == 0UL)
            {
                return true;
            }

            if (!ValidateAddressAndSize(va, size))
            {
                return false;
            }

            int pages = GetPagesCount(va, (uint)size, out va);

            for (int page = 0; page < pages; page++)
            {
                if (!IsMapped(va))
                {
                    return false;
                }

                va += PageSize;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool IsMapped(ulong va)
        {
            if (!ValidateAddress(va))
            {
                return false;
            }

            return _pageTable.Read<ulong>((va / PageSize) * PteSize) != 0;
        }

        private nuint GetPhysicalAddressInternal(ulong va)
        {
            return (nuint)(PteToPa(_pageTable.Read<ulong>((va / PageSize) * PteSize) & ~(0xffffUL << 48)) + (va & PageMask));
        }

        /// <inheritdoc/>
        public void Reprotect(ulong va, ulong size, MemoryPermission protection)
        {
            // TODO
        }

        /// <inheritdoc/>
        public void TrackingReprotect(ulong va, ulong size, MemoryPermission protection, bool guest)
        {
            AssertValidAddressAndSize(va, size);

            if (guest)
            {
                // Protection is inverted on software pages, since the default value is 0.
                protection = (~protection) & MemoryPermission.ReadAndWrite;

                long tag = protection switch
                {
                    MemoryPermission.None => 0L,
                    MemoryPermission.Write => 2L << PointerTagBit,
                    _ => 3L << PointerTagBit,
                };

                int pages = GetPagesCount(va, (uint)size, out va);
                ulong pageStart = va >> PageBits;
                long invTagMask = ~(0xffffL << 48);

                for (int page = 0; page < pages; page++)
                {
                    ref long pageRef = ref _pageTable.GetRef<long>(pageStart * PteSize);

                    long pte;

                    do
                    {
                        pte = Volatile.Read(ref pageRef);
                    }
                    while (pte != 0 && Interlocked.CompareExchange(ref pageRef, (pte & invTagMask) | tag, pte) != pte);

                    pageStart++;
                }
            }
            else
            {
                _pages.TrackingReprotect(va, size, protection);
            }
        }

        /// <inheritdoc/>
        public RegionHandle BeginTracking(ulong address, ulong size, int id, RegionFlags flags = RegionFlags.None)
        {
            return Tracking.BeginTracking(address, size, id, flags);
        }

        /// <inheritdoc/>
        public MultiRegionHandle BeginGranularTracking(ulong address, ulong size, IEnumerable<IRegionHandle> handles, ulong granularity, int id, RegionFlags flags = RegionFlags.None)
        {
            return Tracking.BeginGranularTracking(address, size, handles, granularity, id, flags);
        }

        /// <inheritdoc/>
        public SmartMultiRegionHandle BeginSmartGranularTracking(ulong address, ulong size, ulong granularity, int id)
        {
            return Tracking.BeginSmartGranularTracking(address, size, granularity, id);
        }

        private void SignalMemoryTrackingImpl(ulong va, ulong size, bool write, bool guest, bool precise = false, int? exemptId = null)
        {
            AssertValidAddressAndSize(va, size);

            if (precise)
            {
                Tracking.VirtualMemoryEvent(va, size, write, precise: true, exemptId);
                return;
            }

            // If the memory tracking is coming from the guest, use the tag bits in the page table entry.
            // Otherwise, use the managed page flags.

            if (guest)
            {
                // We emulate guard pages for software memory access. This makes for an easy transition to
                // tracking using host guard pages in future, but also supporting platforms where this is not possible.

                // Write tag includes read protection, since we don't have any read actions that aren't performed before write too.
                long tag = (write ? 3L : 1L) << PointerTagBit;

                int pages = GetPagesCount(va, (uint)size, out _);
                ulong pageStart = va >> PageBits;

                for (int page = 0; page < pages; page++)
                {
                    ref long pageRef = ref _pageTable.GetRef<long>(pageStart * PteSize);

                    long pte = Volatile.Read(ref pageRef);

                    if ((pte & tag) != 0)
                    {
                        Tracking.VirtualMemoryEvent(va, size, write, precise: false, exemptId, true);
                        break;
                    }

                    pageStart++;
                }
            }
            else
            {
                _pages.SignalMemoryTracking(Tracking, va, size, write, exemptId);
            }
        }

        /// <inheritdoc/>
        public override void SignalMemoryTracking(ulong va, ulong size, bool write, bool precise = false, int? exemptId = null)
        {
            SignalMemoryTrackingImpl(va, size, write, false, precise, exemptId);
        }

        private ulong PaToPte(ulong pa)
        {
            return (ulong)_backingMemory.GetPointer(pa, PageSize);
        }

        private ulong PteToPa(ulong pte)
        {
            return (ulong)((long)pte - _backingMemory.Pointer.ToInt64());
        }

        /// <summary>
        /// Disposes of resources used by the memory manager.
        /// </summary>
        protected override void Destroy() => _pageTable.Dispose();

        protected override Memory<byte> GetPhysicalAddressMemory(nuint pa, int size)
            => _backingMemory.GetMemory(pa, size);

        protected override Span<byte> GetPhysicalAddressSpan(nuint pa, int size)
            => _backingMemory.GetSpan(pa, size);

        protected override nuint TranslateVirtualAddressChecked(ulong va)
            => GetPhysicalAddressInternal(va);

        protected override nuint TranslateVirtualAddressUnchecked(ulong va)
            => GetPhysicalAddressInternal(va);
    }
}
