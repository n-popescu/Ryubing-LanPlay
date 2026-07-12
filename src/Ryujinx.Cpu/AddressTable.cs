using ARMeilleure.Memory;
using Ryujinx.Common;
using Ryujinx.Cpu.Signal;
using Ryujinx.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using static Ryujinx.Cpu.MemoryEhMeilleure;

namespace ARMeilleure.Common
{
    /// <summary>
    /// Represents a table of guest address to a value.
    /// </summary>
    /// <typeparam name="TEntry">Type of the value</typeparam>
    public unsafe class AddressTable<TEntry> : IAddressTable<TEntry> where TEntry : unmanaged
    {
        /// <summary>
        /// Represents a page of the address table.
        /// </summary>
        private readonly struct AddressTablePage
        {
            /// <summary>
            /// True if the allocation belongs to a sparse block, false otherwise.
            /// </summary>
            public readonly bool IsSparse;

            /// <summary>
            /// Base address for the page.
            /// </summary>
            public readonly nint Address;

            public AddressTablePage(bool isSparse, nint address)
            {
                IsSparse = isSparse;
                Address = address;
            }
        }

        /// <summary>
        /// A sparsely mapped block of memory with a signal handler to map pages as they're accessed.
        /// </summary>
        private readonly struct TableSparseBlock : IDisposable
        {
            public readonly SparseMemoryBlock Block;
            private readonly TrackingEventDelegate _trackingEvent;

            public TableSparseBlock(ulong size, Action<nint> ensureMapped, PageInitDelegate pageInit)
            {
                SparseMemoryBlock block = new(size, pageInit, null);

                _trackingEvent = (address, size, write) =>
                {
                    ulong pointer = (ulong)block.Block.Pointer + address;
                    ensureMapped((nint)pointer);
                    return pointer;
                };

                bool added = NativeSignalHandler.AddTrackedRegion(
                    (nuint)block.Block.Pointer,
                    (nuint)(block.Block.Pointer + (nint)block.Block.Size),
                    Marshal.GetFunctionPointerForDelegate(_trackingEvent));

                if (!added)
                {
                    throw new InvalidOperationException("Number of allowed tracked regions exceeded.");
                }

                Block = block;
            }

            public void Dispose()
            {
                NativeSignalHandler.RemoveTrackedRegion((nuint)Block.Block.Pointer);

                Block.Dispose();
            }
        }

        private bool _disposed;
        private ulong _sparseBoundsStart;
        private ulong _sparseBoundsEnd;
        private TEntry** _table;
        private TEntry* _sparseTable;
        private readonly List<AddressTablePage> _pages;
        private TEntry _fill;

        private TableSparseBlock _sparseReserved;

        private ulong _sparseBlockSize;

        public bool Sparse { get; }

        /// <inheritdoc/>
        public ulong Mask { get; }

        /// <inheritdoc/>
        public AddressTableLevel[] Levels { get; }

        /// <inheritdoc/>
        public TEntry Fill
        {
            get
            {
                return _fill;
            }
            set
            {
                UpdateFill(value);
            }
        }

        /// <inheritdoc/>
        public nint Base
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (Sparse)
                {
                    return (nint)_sparseTable;
                }

                lock (_pages)
                {
                    return (nint)GetRootPage();
                }
            }
        }

        /// <summary>
        /// Constructs a new instance of the <see cref="AddressTable{TEntry}"/> class with the specified list of
        /// <see cref="AddressTableLevel"/>.
        /// </summary>
        /// <param name="levels">Levels for the address table</param>
        /// <param name="sparse">True if the bottom page should be sparsely mapped</param>
        /// <exception cref="ArgumentNullException"><paramref name="levels"/> is null</exception>
        /// <exception cref="ArgumentException">Length of <paramref name="levels"/> is less than 2</exception>
        public AddressTable(AddressTableLevel[] levels, bool sparse)
        {
            ArgumentNullException.ThrowIfNull(levels);

            _pages = new List<AddressTablePage>(capacity: 16);

            Levels = levels;
            Mask = 0;

            foreach (AddressTableLevel level in Levels)
            {
                Mask |= level.Mask;
            }

            Sparse = sparse;
        }

        /// <summary>
        /// Create an <see cref="AddressTable{TEntry}"/> instance for an ARM function table.
        /// Selects the best table structure for A32/A64, taking into account the selected memory manager type.
        /// </summary>
        /// <param name="for64Bits">True if the guest is A64, false otherwise</param>
        /// <param name="type">Memory manager type</param>
        /// <returns>An <see cref="AddressTable{TEntry}"/> for ARM function lookup</returns>
        public static AddressTable<TEntry> CreateForArm(bool for64Bits, MemoryManagerType type)
        {
            // Assume software memory means that we don't want to use any signal handlers.
            bool sparse = type is not MemoryManagerType.SoftwareMmu and not MemoryManagerType.SoftwarePageTable;

            return new AddressTable<TEntry>(AddressTablePresets.GetArmPreset(for64Bits, sparse), sparse);
        }

        /// <summary>
        /// Update the fill value for the bottom level of the table.
        /// </summary>
        /// <param name="fillValue">New fill value</param>
        private void UpdateFill(TEntry fillValue)
        {
            _fill = fillValue;
        }

        /// <summary>
        /// Signal that the given code range exists.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="size"></param>
        public void SignalCodeRange(ulong address, ulong size)
        {
            AddressTableLevel bottom = Levels.Last();
            
            ulong entries = size >> bottom.Index;
            
            if (Sparse)
            {
                _sparseBoundsStart = address;
                _sparseBoundsEnd = address + size;

                _sparseBlockSize = entries * (ulong)sizeof(TEntry);
                
                _sparseTable = (TEntry*)Allocate((int)entries, Fill, leaf: true);

                _sparseTable -= Levels.Last().GetValue(address);
            }
        }

        /// <inheritdoc/>
        public bool IsValid(ulong address)
        {
            if (Sparse)
            {
                if ((address & ~Mask) == 0)
                {
                    if (address >= _sparseBoundsStart && address < _sparseBoundsEnd)
                    {
                        return true;
                    }

                    throw new IndexOutOfRangeException($"requested address was ({address}), but the valid range is only ({_sparseBoundsStart} - {_sparseBoundsEnd})");
                }

                return false;
            }
            
            return (address & ~Mask) == 0;
        }

        /// <inheritdoc/>
        public ref TEntry GetValue(ulong address)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsValid(address))
            {
                throw new ArgumentException($"Address 0x{address:X} is not mapped onto the table.", nameof(address));
            }

            if (Sparse)
            {
                long index = Levels.Last().GetValue(address);
                
                EnsureMapped((nint)(_sparseTable + index));
                
                return ref _sparseTable[index];
            }

            lock (_pages)
            {
                TEntry* page = GetPage(address);

                long index = Levels.Last().GetValue(address);

                EnsureMapped((nint)(page + index));

                return ref page[index];
            }
        }

        /// <summary>
        /// Gets the leaf page for the specified guest <paramref name="address"/>.
        /// </summary>
        /// <param name="address">Guest address</param>
        /// <returns>Leaf page for the specified guest <paramref name="address"/></returns>
        private TEntry* GetPage(ulong address)
        {
            TEntry** page = GetRootPage();

            for (int i = 0; i < Levels.Length - 1; i++)
            {
                ref AddressTableLevel level = ref Levels[i];
                ref TEntry* nextPage = ref page[level.GetValue(address)];

                if (nextPage == null)
                {
                    ref AddressTableLevel nextLevel = ref Levels[i + 1];

                    if (i == Levels.Length - 2)
                    {
                        nextPage = (TEntry*)Allocate(1 << nextLevel.Length, Fill, leaf: true);
                    }
                    else
                    {
                        nextPage = (TEntry*)Allocate(1 << nextLevel.Length, nint.Zero, leaf: false);
                    }
                }

                page = (TEntry**)nextPage;
            }

            return (TEntry*)page;
        }

        /// <summary>
        /// Ensure the given pointer is mapped in any overlapping sparse reservations.
        /// </summary>
        /// <param name="ptr">Pointer to be mapped</param>
        private void EnsureMapped(nint ptr)
        {
            if (Sparse)
            {
                SparseMemoryBlock sparse = _sparseReserved.Block;

                if (ptr >= sparse.Block.Pointer && ptr < sparse.Block.Pointer + (nint)sparse.Block.Size)
                {
                    sparse.EnsureMapped((ulong)(ptr - sparse.Block.Pointer));
                }
            }
        }

        /// <summary>
        /// Lazily initialize and get the root page of the <see cref="AddressTable{TEntry}"/>.
        /// </summary>
        /// <returns>Root page of the <see cref="AddressTable{TEntry}"/></returns>
        private TEntry** GetRootPage()
        {
            if (_table == null)
            {
                if (Levels.Length == 1)
                    _table = (TEntry**)Allocate(1 << Levels[0].Length, Fill, leaf: true);
                else
                    _table = (TEntry**)Allocate(1 << Levels[0].Length, nint.Zero, leaf: false);
            }

            return _table;
        }

        /// <summary>
        /// Initialize a leaf page with the fill value.
        /// </summary>
        /// <param name="page">Page to initialize</param>
        private void InitLeafPage(Span<byte> page)
        {
            MemoryMarshal.Cast<byte, TEntry>(page).Fill(_fill);
        }

        /// <summary>
        /// Reserve a new sparse block, and add it to the list.
        /// </summary>
        /// <returns>The new sparse block that was added</returns>
        private TableSparseBlock ReserveNewSparseBlock()
        {
            TableSparseBlock block = new(_sparseBlockSize, EnsureMapped, InitLeafPage);

            _sparseReserved = block;

            return block;
        }

        /// <summary>
        /// Allocates a block of memory of the specified type and length.
        /// </summary>
        /// <typeparam name="T">Type of elements</typeparam>
        /// <param name="length">Number of elements</param>
        /// <param name="fill">Fill value</param>
        /// <param name="leaf"><see langword="true"/> if leaf; otherwise <see langword="false"/></param>
        /// <returns>Allocated block</returns>
        private nint Allocate<T>(int length, T fill, bool leaf) where T : unmanaged
        {
            int size = sizeof(T) * length;

            AddressTablePage page;

            if (Sparse && leaf)
            {
                if (_sparseReserved.Block != null)
                {
                    throw new InvalidOperationException();
                    
                }
                
                SparseMemoryBlock block = ReserveNewSparseBlock().Block;

                page = new AddressTablePage(true, block.Block.Pointer);
            }
            else
            {
                nint address = (nint)NativeAllocator.Instance.Allocate((uint)size);
                page = new AddressTablePage(false, address);

                Span<T> span = new((void*)page.Address, length);
                span.Fill(fill);
            }

            _pages.Add(page);

            //TranslatorEventSource.Log.AddressTableAllocated(size, leaf);

            return page.Address;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="AddressTable{TEntry}"/> instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all unmanaged and optionally managed resources used by the <see cref="AddressTable{TEntry}"/>
        /// instance.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to dispose managed resources also; otherwise just unmanaged resouces</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                foreach (AddressTablePage page in _pages)
                {
                    if (!page.IsSparse)
                    {
                        Marshal.FreeHGlobal(page.Address);
                    }
                }

                if (Sparse)
                {
                    _sparseReserved.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Frees resources used by the <see cref="AddressTable{TEntry}"/> instance.
        /// </summary>
        ~AddressTable()
        {
            Dispose(false);
        }
    }
}
