﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProtoBuf
{
    public partial class ProtoWriter
    {
        /// <summary>
        /// Gets the default state associated with this writer
        /// </summary>
        protected internal abstract State DefaultState();

        /// <summary>
        /// Writer state
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public ref partial struct State
        {
            internal bool IsActive => !_span.IsEmpty;

            private Span<byte> _span;
            private Memory<byte> _memory;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal State(ProtoWriter writer)
            {
                this = default;
                _writer = writer;
            }

#pragma warning disable IDE0044, IDE0052 // make readonly, remove unused
            private ProtoWriter _writer;
#pragma warning restore IDE0044, IDE0052 // make readonly, remove unused

            internal Span<byte> Remaining
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _span.Slice(OffsetInCurrent);
            }

            internal int RemainingInCurrent { get; private set; }
            internal int OffsetInCurrent { get; private set; }

            internal void Init(Memory<byte> memory)
            {
                _memory = memory;
                _span = memory.Span;
                RemainingInCurrent = _span.Length;
            }

            /// <summary>
            /// Writes any uncommitted data to the output
            /// </summary>
            public void Flush()
            {
                if (_writer.TryFlush(ref this))
                {
                    _writer._needFlush = false;
                }
            }

            internal int ConsiderWritten()
            {
                int val = OffsetInCurrent;
                var writer = _writer;
                this = default;
                _writer = writer;
                return val;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void LocalWriteFixed32(uint value)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(Remaining, value);
                OffsetInCurrent += 4;
                RemainingInCurrent -= 4;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ReverseLast32() => _span.Slice(OffsetInCurrent - 4, 4).Reverse();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void LocalAdvance(int bytes)
            {
                OffsetInCurrent += bytes;
                RemainingInCurrent -= bytes;
            }

            internal void LocalWriteBytes(ReadOnlySpan<byte> span)
            {
                span.CopyTo(Remaining);
                OffsetInCurrent += span.Length;
                RemainingInCurrent -= span.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void LocalWriteFixed64(ulong value)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(Remaining, value);
                OffsetInCurrent += 8;
                RemainingInCurrent -= 8;
            }

            internal void LocalWriteString(string value)
            {
                int bytes;
#if PLAT_SPAN_OVERLOADS
                bytes = UTF8.GetBytes(value.AsSpan(), Remaining);
#else
                unsafe
                {
                    fixed (char* cPtr = value)
                    {
                        fixed (byte* bPtr = &MemoryMarshal.GetReference(_span))
                        {
                            bytes = UTF8.GetBytes(cPtr, value.Length,
                                bPtr + OffsetInCurrent, RemainingInCurrent);
                        }
                    }
                }
#endif
                OffsetInCurrent += bytes;
                RemainingInCurrent -= bytes;
            }

            internal int LocalWriteVarint64(ulong value)
            {
                int count = 0;
                var span = _span;
                var index = OffsetInCurrent;
                do
                {
                    span[index++] = (byte)((value & 0x7F) | 0x80);
                    count++;
                } while ((value >>= 7) != 0);
                span[index - 1] &= 0x7F;

                OffsetInCurrent += count;
                RemainingInCurrent -= count;
                return count;
            }

            internal int ReadFrom(Stream source)
            {
                int bytes;
                if (MemoryMarshal.TryGetArray<byte>(_memory, out var segment))
                {
                    bytes = source.Read(segment.Array, segment.Offset + OffsetInCurrent, RemainingInCurrent);
                }
                else
                {
#if PLAT_SPAN_OVERLOADS
                    bytes = source.Read(Remaining);
#else
                    var arr = System.Buffers.ArrayPool<byte>.Shared.Rent(RemainingInCurrent);
                    bytes = source.Read(arr, 0, RemainingInCurrent);
                    if (bytes > 0) new Span<byte>(arr, 0, bytes).CopyTo(Remaining);
                    System.Buffers.ArrayPool<byte>.Shared.Return(arr);
#endif
                }
                if (bytes > 0)
                {
                    OffsetInCurrent += bytes;
                    RemainingInCurrent -= bytes;
                }
                return bytes;
            }

            internal int LocalWriteVarint32(uint value)
            {
                int count = 0;
                var span = _span;
                var index = OffsetInCurrent;
                do
                {
                    span[index++] = (byte)((value & 0x7F) | 0x80);
                    count++;
                } while ((value >>= 7) != 0);
                span[index - 1] &= 0x7F;

                OffsetInCurrent += count;
                RemainingInCurrent -= count;
                return count;
            }
        }
    }
}
