﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Garnet.common;

namespace Garnet.server
{
    internal static class CustomCommandUtils
    {
        /// <summary>
        /// Shared memory pool used by functions
        /// </summary>
        private static MemoryPool<byte> MemoryPool => MemoryPool<byte>.Shared;

        /// <summary>
        /// Get first arg from input
        /// </summary>
        /// <param name="input">Object store input</param>
        /// <returns></returns>
        public static ReadOnlySpan<byte> GetFirstArg(ref ObjectInput input)
        {
            var idx = 0;
            return GetNextArg(ref input, ref idx);
        }

        /// <summary>
        /// Get first arg from input
        /// </summary>
        /// <param name="input">Main store input</param>
        /// <returns></returns>
        public static ReadOnlySpan<byte> GetFirstArg(ref RawStringInput input)
        {
            var idx = 0;
            return GetNextArg(ref input, ref idx);
        }

        /// <summary>
        /// Get argument from input, at specified index (starting from 0)
        /// </summary>
        /// <param name="input">Object store input</param>
        /// <param name="idx">Current argument index in input</param>
        /// <returns>Argument as a span</returns>
        public static ReadOnlySpan<byte> GetNextArg(ref ObjectInput input, scoped ref int idx)
        {
            var arg = idx < input.parseState.Count
                ? input.parseState.GetArgSliceByRef(idx).ReadOnlySpan
                : default;
            idx++;
            return arg;
        }

        /// <summary>
        /// Get argument from input, at specified index (starting from 0)
        /// </summary>
        /// <param name="input">Main store input</param>
        /// <param name="idx">Current argument index in input</param>
        /// <returns>Argument as a span</returns>
        public static ReadOnlySpan<byte> GetNextArg(ref RawStringInput input, scoped ref int idx)
        {
            var arg = idx < input.parseState.Count
                ? input.parseState.GetArgSliceByRef(idx).ReadOnlySpan
                : default;
            idx++;
            return arg;
        }

        /// <summary>
        /// Create output as bulk string, from given Span
        /// </summary>
        public static unsafe void WriteBulkString(ref (IMemoryOwner<byte>, int) output, Span<byte> bulkString)
        {
            // Get space for bulk string
            var len = RespWriteUtils.GetBulkStringLength(bulkString.Length);
            output.Item1 = MemoryPool.Rent(len);
            output.Item2 = len;
            fixed (byte* ptr = output.Item1.Memory.Span)
            {
                var curr = ptr;
                // NOTE: Expected to always have enough space to write into pre-allocated buffer
                var success = RespWriteUtils.TryWriteBulkString(bulkString, ref curr, ptr + len);
                Debug.Assert(success, "Insufficient space in pre-allocated buffer");
            }
        }

        /// <summary>
        /// Create output as bulk string, from given Span
        /// </summary>
        public static unsafe void WriteBulkString(ref (IMemoryOwner<byte>, int) output, IEnumerable<byte[]> bulkStrings)
        {
            // Get space for bulk string
            var stringLen = bulkStrings.Sum(x => x.Length);
            var len = RespWriteUtils.GetBulkStringLength(stringLen);
            output.Item1 = MemoryPool.Rent(len);
            output.Item2 = len;
            fixed (byte* ptr = output.Item1.Memory.Span)
            {
                var curr = ptr;
                // NOTE: Expected to always have enough space to write into pre-allocated buffer
                var success = RespWriteUtils.TryWriteBulkString(bulkStrings, stringLen, ref curr, ptr + len);
                Debug.Assert(success, "Insufficient space in pre-allocated buffer");
            }
        }

        /// <summary>
        /// Create output as error message, from given string
        /// </summary>
        public static unsafe void WriteError(ref (IMemoryOwner<byte>, int) output, string errorMessage)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(errorMessage);
            WriteError(ref output, bytes);
        }

        /// <summary>
        /// Create output as error message, from given byte span
        /// </summary>
        /// <param name="output">A tuple containing the memory owner and the length of written data</param>
        /// <param name="errorMessage">The error message to write</param>
        public static unsafe void WriteError(ref (IMemoryOwner<byte>, int) output, ReadOnlySpan<byte> errorMessage)
        {
            var bytes = errorMessage;
            // Get space for error
            var len = 1 + bytes.Length + 2;
            output.Item1 = MemoryPool.Rent(len);
            fixed (byte* ptr = output.Item1.Memory.Span)
            {
                var curr = ptr;
                // NOTE: Expected to always have enough space to write into pre-allocated buffer
                var success = RespWriteUtils.TryWriteError(bytes, ref curr, ptr + len);
                Debug.Assert(success, "Insufficient space in pre-allocated buffer");
            }
            output.Item2 = len;
        }

        /// <summary>
        /// Create null output as bulk string
        /// </summary>
        public static unsafe void WriteNullBulkString(ref (IMemoryOwner<byte>, int) output)
        {
            // Get space for null bulk string "$-1\r\n"
            var len = 5;
            output.Item1 = MemoryPool.Rent(len);
            output.Item2 = len;
            fixed (byte* ptr = output.Item1.Memory.Span)
            {
                var curr = ptr;
                // NOTE: Expected to always have enough space to write into pre-allocated buffer
                var success = RespWriteUtils.TryWriteNull(ref curr, ptr + len);
                Debug.Assert(success, "Insufficient space in pre-allocated buffer");
            }
        }

        /// <summary>
        /// Create output as simple string, from given string
        /// </summary>
        public static unsafe void WriteSimpleString(ref (IMemoryOwner<byte>, int) output, string simpleString)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(simpleString);
            // Get space for simple string
            var len = 1 + bytes.Length + 2;
            output.Item1 = MemoryPool.Rent(len);
            fixed (byte* ptr = output.Item1.Memory.Span)
            {
                var curr = ptr;
                // NOTE: Expected to always have enough space to write into pre-allocated buffer
                var success = RespWriteUtils.TryWriteSimpleString(bytes, ref curr, ptr + len);
                Debug.Assert(success, "Insufficient space in pre-allocated buffer");
            }
            output.Item2 = len;
        }

        /// <summary>
        /// Writes bytes directly into a rented memory buffer without any encoding transformation.
        /// </summary>
        /// <param name="output">A tuple containing the memory owner and the length of written data.</param>
        /// <param name="bytes">The source bytes to write.</param>
        public static unsafe void WriteDirect(ref (IMemoryOwner<byte>, int) output, ReadOnlySpan<byte> bytes)
        {
            output.Item1 = MemoryPool.Rent(bytes.Length);
            fixed (byte* ptr = output.Item1.Memory.Span)
            {
                var curr = ptr;
                // NOTE: Expected to always have enough space to write into pre-allocated buffer
                var success = RespWriteUtils.TryWriteDirect(bytes, ref curr, ptr + bytes.Length);
                Debug.Assert(success, "Insufficient space in pre-allocated buffer");
            }
            output.Item2 = bytes.Length;
        }
    }
}