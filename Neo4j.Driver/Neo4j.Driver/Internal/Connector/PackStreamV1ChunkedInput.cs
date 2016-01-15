﻿//  Copyright (c) 2002-2016 "Neo Technology,"
//  Network Engine for Objects in Lund AB [http://neotechnology.com]
// 
//  This file is part of Neo4j.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
using System;
using System.Collections.Generic;
using Sockets.Plugin.Abstractions;

namespace Neo4j.Driver
{
    public class PackStreamV1ChunkedInput : IChunkedInput
    {
        private const int ChunkSize = 1024*8; // TODO: 2 * chunk_size of server
        public static byte[] Tail = {0x00, 0x00};
        private readonly BitConverterBase _bitConverter;
        private readonly Queue<byte> _chunkBuffer = new Queue<byte>(ChunkSize); // TODO
        private readonly byte[] _headTailBuffer = new byte[2];
        private readonly ITcpSocketClient _tcpSocketClient;

        public PackStreamV1ChunkedInput(ITcpSocketClient tcpSocketClient, BitConverterBase bitConverter)
        {
            _tcpSocketClient = tcpSocketClient;
            _bitConverter = bitConverter;
        }

        public sbyte ReadSByte()
        {
            Ensure(1);
            return (sbyte) _chunkBuffer.Dequeue();
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _chunkBuffer.Dequeue();
        }

        public short ReadShort()
        {
            throw new NotImplementedException();
        }

        public int ReadInt()
        {
            throw new NotImplementedException();
        }

        public void ReadBytes(byte[] buffer, int offset = 0, int? length = null)
        {
            if (length == null)
                length = buffer.Length;

            Ensure(length.Value);
            for (int i = 0; i < length.Value; i++)
            {
                buffer[i+offset] = _chunkBuffer.Dequeue();
            }
        }

        public byte PeekByte()
        {
            Ensure(1);
            return _chunkBuffer.Peek();
        }

        private void Ensure(int size)
        {
            while (_chunkBuffer.Count < size)
            {
                // head
                ReadSpecifiedSize(_headTailBuffer);

                var chunkSize = _bitConverter.ToInt16(_headTailBuffer);

                // chunk
                var chunk = new byte[chunkSize]; // TODO use a single buffer somehow
                ReadSpecifiedSize(chunk);
                for (var i = 0; i < chunkSize; i ++)
                {
                    _chunkBuffer.Enqueue(chunk[i]);
                }

            }
        }

        private void ReadSpecifiedSize(byte[] buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }
            var numberOfbytesRead = _tcpSocketClient.ReadStream.Read(buffer);
            if (numberOfbytesRead != buffer.Length)
            {
                //TODO: Convert to Neo4j Exception.
                throw new InvalidOperationException($"Expect {buffer.Length}, but got {numberOfbytesRead}");
            }
        }

        public void ReadMessageEnding()
        {
            // tail 00 00 
            ReadSpecifiedSize(_headTailBuffer);
            if (_headTailBuffer.Equals(Tail))
            {
                //TODO: Convert to Neo4j Exception.
                throw new InvalidOperationException("Not chunked correctly");
            }
        }
    }
}