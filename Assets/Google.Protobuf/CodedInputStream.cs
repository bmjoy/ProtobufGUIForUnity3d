#region Copyright notice and license

// Protocol Buffers - Google's data interchange format
// Copyright 2008 Google Inc.  All rights reserved.
// https://developers.google.com/protocol-buffers/
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//
//     * Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above
// copyright notice, this list of conditions and the following disclaimer
// in the documentation and/or other materials provided with the
// distribution.
//     * Neither the name of Google Inc. nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf.Collections;

namespace Google.Protobuf
{
    /// <summary>
    ///     Reads and decodes protocol message fields.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This class is generally used by generated code to read appropriate
    ///         primitives from the stream. It effectively encapsulates the lowest
    ///         levels of protocol buffer format.
    ///     </para>
    ///     <para>
    ///         Repeated fields and map fields are not handled by this class; use <see cref="RepeatedField{T}" />
    ///         and <see cref="MapField{TKey, TValue}" /> to serialize such fields.
    ///     </para>
    /// </remarks>
    public sealed class CodedInputStream : IDisposable
    {
        internal const int DefaultRecursionLimit = 64;
        internal const int DefaultSizeLimit = int.MaxValue;
        internal const int BufferSize = 4096;

        /// <summary>
        ///     Buffer of data read from the stream or provided at construction time.
        /// </summary>
        private readonly byte [ ] buffer;

        /// <summary>
        ///     The stream to read further input from, or null if the byte array buffer was provided
        ///     directly on construction, with no further data available.
        /// </summary>
        private readonly Stream input;

        /// <summary>
        ///     Whether to leave the underlying stream open when disposing of this stream.
        ///     This is always true when there's no stream.
        /// </summary>
        private readonly bool leaveOpen;

        /// <summary>
        ///     The position within the current buffer (i.e. the next byte to read)
        /// </summary>
        private int bufferPos;

        /// <summary>
        ///     The index of the buffer at which we need to refill from the stream (if there is one).
        /// </summary>
        private int bufferSize;

        private int bufferSizeAfterLimit;

        /// <summary>
        ///     The absolute position of the end of the current message.
        /// </summary>
        private int currentLimit = int.MaxValue;

        private bool hasNextTag;

        /// <summary>
        ///     The last tag we read. 0 indicates we've read to the end of the stream
        ///     (or haven't read anything yet).
        /// </summary>
        private uint lastTag;

        /// <summary>
        ///     The next tag, used to store the value read by PeekTag.
        /// </summary>
        private uint nextTag;

        private int recursionDepth;

        /// <summary>
        ///     The total number of bytes read before the current buffer. The
        ///     total bytes read up to the current position can be computed as
        ///     totalBytesRetired + bufferPos.
        /// </summary>
        private int totalBytesRetired;

        /// <summary>
        ///     Returns the current position in the input stream, or the position in the input buffer
        /// </summary>
        public long Position
        {
            get
            {
                if ( this.input != null )
                    return this.input.Position - ( this.bufferSize + this.bufferSizeAfterLimit - this.bufferPos );
                return this.bufferPos;
            }
        }

        /// <summary>
        ///     Returns the last tag read, or 0 if no tags have been read or we've read beyond
        ///     the end of the stream.
        /// </summary>
        internal uint LastTag => this.lastTag;

        /// <summary>
        ///     Returns the size limit for this stream.
        /// </summary>
        /// <remarks>
        ///     This limit is applied when reading from the underlying stream, as a sanity check. It is
        ///     not applied when reading from a byte array data source without an underlying stream.
        ///     The default value is Int32.MaxValue.
        /// </remarks>
        /// <value>
        ///     The size limit.
        /// </value>
        public int SizeLimit { get; }

        /// <summary>
        ///     Returns the recursion limit for this stream. This limit is applied whilst reading messages,
        ///     to avoid maliciously-recursive data.
        /// </summary>
        /// <remarks>
        ///     The default limit is 64.
        /// </remarks>
        /// <value>
        ///     The recursion limit for this stream.
        /// </value>
        public int RecursionLimit { get; }

        /// <summary>
        ///     Internal-only property; when set to true, unknown fields will be discarded while parsing.
        /// </summary>
        internal bool DiscardUnknownFields { get; set; }

        /// <summary>
        ///     Disposes of this instance, potentially closing any underlying stream.
        /// </summary>
        /// <remarks>
        ///     As there is no flushing to perform here, disposing of a <see cref="CodedInputStream" /> which
        ///     was constructed with the <c>leaveOpen</c> option parameter set to <c>true</c> (or one which
        ///     was constructed to read from a byte array) has no effect.
        /// </remarks>
        public void Dispose()
        {
            if ( !this.leaveOpen )
                this.input.Dispose();
        }

        /// <summary>
        ///     Creates a <see cref="CodedInputStream" /> with the specified size and recursion limits, reading
        ///     from an input stream.
        /// </summary>
        /// <remarks>
        ///     This method exists separately from the constructor to reduce the number of constructor overloads.
        ///     It is likely to be used considerably less frequently than the constructors, as the default limits
        ///     are suitable for most use cases.
        /// </remarks>
        /// <param name="input">The input stream to read from</param>
        /// <param name="sizeLimit">The total limit of data to read from the stream.</param>
        /// <param name="recursionLimit">The maximum recursion depth to allow while reading.</param>
        /// <returns>
        ///     A <c>CodedInputStream</c> reading from <paramref name="input" /> with the specified size
        ///     and recursion limits.
        /// </returns>
        public static CodedInputStream CreateWithLimits ( Stream input , int sizeLimit , int recursionLimit )
        {
            // Note: we may want an overload accepting leaveOpen
            return new CodedInputStream( input , new byte[ BufferSize ] , 0 , 0 , sizeLimit , recursionLimit , false );
        }

        #region Validation

        /// <summary>
        ///     Verifies that the last call to ReadTag() returned tag 0 - in other words,
        ///     we've reached the end of the stream when we expected to.
        /// </summary>
        /// <exception cref="InvalidProtocolBufferException">
        ///     The
        ///     tag read was not the one specified
        /// </exception>
        internal void CheckReadEndOfStreamTag()
        {
            if ( this.lastTag != 0 )
                throw InvalidProtocolBufferException.MoreDataAvailable();
        }

        #endregion

        #region Construction

        // Note that the checks are performed such that we don't end up checking obviously-valid things
        // like non-null references for arrays we've just created.

        /// <summary>
        ///     Creates a new CodedInputStream reading data from the given byte array.
        /// </summary>
        public CodedInputStream ( byte [ ] buffer ) : this( null , ProtoPreconditions.CheckNotNull( buffer , "buffer" ) , 0 , buffer.Length , true )
        {
        }

        /// <summary>
        ///     Creates a new <see cref="CodedInputStream" /> that reads from the given byte array slice.
        /// </summary>
        public CodedInputStream ( byte [ ] buffer , int offset , int length )
            : this( null , ProtoPreconditions.CheckNotNull( buffer , "buffer" ) , offset , offset + length , true )
        {
            if ( offset < 0 || offset > buffer.Length )
                throw new ArgumentOutOfRangeException( "offset" , "Offset must be within the buffer" );
            if ( length < 0 || offset + length > buffer.Length )
                throw new ArgumentOutOfRangeException( "length" , "Length must be non-negative and within the buffer" );
        }

        /// <summary>
        ///     Creates a new <see cref="CodedInputStream" /> reading data from the given stream, which will be disposed
        ///     when the returned object is disposed.
        /// </summary>
        /// <param name="input">The stream to read from.</param>
        public CodedInputStream ( Stream input ) : this( input , false )
        {
        }

        /// <summary>
        ///     Creates a new <see cref="CodedInputStream" /> reading data from the given stream.
        /// </summary>
        /// <param name="input">The stream to read from.</param>
        /// <param name="leaveOpen">
        ///     <c>true</c> to leave <paramref name="input" /> open when the returned
        ///     <c cref="CodedInputStream" /> is disposed; <c>false</c> to dispose of the given stream when the
        ///     returned object is disposed.
        /// </param>
        public CodedInputStream ( Stream input , bool leaveOpen )
            : this( ProtoPreconditions.CheckNotNull( input , "input" ) , new byte[ BufferSize ] , 0 , 0 , leaveOpen )
        {
        }

        /// <summary>
        ///     Creates a new CodedInputStream reading data from the given
        ///     stream and buffer, using the default limits.
        /// </summary>
        internal CodedInputStream ( Stream input , byte [ ] buffer , int bufferPos , int bufferSize , bool leaveOpen )
        {
            this.input = input;
            this.buffer = buffer;
            this.bufferPos = bufferPos;
            this.bufferSize = bufferSize;
            this.SizeLimit = DefaultSizeLimit;
            this.RecursionLimit = DefaultRecursionLimit;
            this.leaveOpen = leaveOpen;
        }

        /// <summary>
        ///     Creates a new CodedInputStream reading data from the given
        ///     stream and buffer, using the specified limits.
        /// </summary>
        /// <remarks>
        ///     This chains to the version with the default limits instead of vice versa to avoid
        ///     having to check that the default values are valid every time.
        /// </remarks>
        internal CodedInputStream ( Stream input , byte [ ] buffer , int bufferPos , int bufferSize , int sizeLimit , int recursionLimit , bool leaveOpen )
            : this( input , buffer , bufferPos , bufferSize , leaveOpen )
        {
            if ( sizeLimit <= 0 )
                throw new ArgumentOutOfRangeException( "sizeLimit" , "Size limit must be positive" );
            if ( recursionLimit <= 0 )
                throw new ArgumentOutOfRangeException( "recursionLimit!" , "Recursion limit must be positive" );
            this.SizeLimit = sizeLimit;
            this.RecursionLimit = recursionLimit;
        }

        #endregion

        #region Reading of tags etc

        /// <summary>
        ///     Peeks at the next field tag. This is like calling <see cref="ReadTag" />, but the
        ///     tag is not consumed. (So a subsequent call to <see cref="ReadTag" /> will return the
        ///     same value.)
        /// </summary>
        public uint PeekTag()
        {
            if ( this.hasNextTag )
                return this.nextTag;

            var savedLast = this.lastTag;
            this.nextTag = this.ReadTag();
            this.hasNextTag = true;
            this.lastTag = savedLast; // Undo the side effect of ReadTag
            return this.nextTag;
        }

        /// <summary>
        ///     Reads a field tag, returning the tag of 0 for "end of stream".
        /// </summary>
        /// <remarks>
        ///     If this method returns 0, it doesn't necessarily mean the end of all
        ///     the data in this CodedInputStream; it may be the end of the logical stream
        ///     for an embedded message, for example.
        /// </remarks>
        /// <returns>The next field tag, or 0 for end of stream. (0 is never a valid tag.)</returns>
        public uint ReadTag()
        {
            if ( this.hasNextTag )
            {
                this.lastTag = this.nextTag;
                this.hasNextTag = false;
                return this.lastTag;
            }

            // Optimize for the incredibly common case of having at least two bytes left in the buffer,
            // and those two bytes being enough to get the tag. This will be true for fields up to 4095.
            if ( this.bufferPos + 2 <= this.bufferSize )
            {
                int tmp = this.buffer [ this.bufferPos++ ];
                if ( tmp < 128 )
                {
                    this.lastTag = ( uint ) tmp;
                }
                else
                {
                    var result = tmp & 0x7f;
                    if ( ( tmp = this.buffer [ this.bufferPos++ ] ) < 128 )
                    {
                        result |= tmp << 7;
                        this.lastTag = ( uint ) result;
                    }
                    else
                    {
                        // Nope, rewind and go the potentially slow route.
                        this.bufferPos -= 2;
                        this.lastTag = this.ReadRawVarint32();
                    }
                }
            }
            else
            {
                if ( this.IsAtEnd )
                {
                    this.lastTag = 0;
                    return 0; // This is the only case in which we return 0.
                }

                this.lastTag = this.ReadRawVarint32();
            }

            if ( WireFormat.GetTagFieldNumber( this.lastTag ) == 0 )
                throw InvalidProtocolBufferException.InvalidTag();
            return this.lastTag;
        }

        /// <summary>
        ///     Skips the data for the field with the tag we've just read.
        ///     This should be called directly after <see cref="ReadTag" />, when
        ///     the caller wishes to skip an unknown field.
        /// </summary>
        /// <remarks>
        ///     This method throws <see cref="InvalidProtocolBufferException" /> if the last-read tag was an end-group tag.
        ///     If a caller wishes to skip a group, they should skip the whole group, by calling this method after reading the
        ///     start-group tag. This behavior allows callers to call this method on any field they don't understand, correctly
        ///     resulting in an error if an end-group tag has not been paired with an earlier start-group tag.
        /// </remarks>
        /// <exception cref="InvalidProtocolBufferException">The last tag was an end-group tag</exception>
        /// <exception cref="InvalidOperationException">The last read operation read to the end of the logical stream</exception>
        public void SkipLastField()
        {
            if ( this.lastTag == 0 )
                throw new InvalidOperationException( "SkipLastField cannot be called at the end of a stream" );
            switch ( WireFormat.GetTagWireType( this.lastTag ) )
            {
                case WireFormat.WireType.StartGroup :
                    this.SkipGroup( this.lastTag );
                    break;
                case WireFormat.WireType.EndGroup :
                    throw new InvalidProtocolBufferException(
                        "SkipLastField called on an end-group tag, indicating that the corresponding start-group was missing" );
                case WireFormat.WireType.Fixed32 :
                    this.ReadFixed32();
                    break;
                case WireFormat.WireType.Fixed64 :
                    this.ReadFixed64();
                    break;
                case WireFormat.WireType.LengthDelimited :
                    var length = this.ReadLength();
                    this.SkipRawBytes( length );
                    break;
                case WireFormat.WireType.Varint :
                    this.ReadRawVarint32();
                    break;
            }
        }

        /// <summary>
        ///     Skip a group.
        /// </summary>
        internal void SkipGroup ( uint startGroupTag )
        {
            // Note: Currently we expect this to be the way that groups are read. We could put the recursion
            // depth changes into the ReadTag method instead, potentially...
            this.recursionDepth++;
            if ( this.recursionDepth >= this.RecursionLimit )
                throw InvalidProtocolBufferException.RecursionLimitExceeded();
            uint tag;
            while ( true )
            {
                tag = this.ReadTag();
                if ( tag == 0 )
                    throw InvalidProtocolBufferException.TruncatedMessage();
                // Can't call SkipLastField for this case- that would throw.
                if ( WireFormat.GetTagWireType( tag ) == WireFormat.WireType.EndGroup )
                    break;
                // This recursion will allow us to handle nested groups.
                this.SkipLastField();
            }

            var startField = WireFormat.GetTagFieldNumber( startGroupTag );
            var endField = WireFormat.GetTagFieldNumber( tag );
            if ( startField != endField )
                throw new InvalidProtocolBufferException(
                    $"Mismatched end-group tag. Started with field {startField}; ended with field {endField}" );
            this.recursionDepth--;
        }

        /// <summary>
        ///     Reads a double field from the stream.
        /// </summary>
        public double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble( ( long ) this.ReadRawLittleEndian64() );
        }

        /// <summary>
        ///     Reads a float field from the stream.
        /// </summary>
        public float ReadFloat()
        {
            if ( BitConverter.IsLittleEndian && 4 <= this.bufferSize - this.bufferPos )
            {
                var ret = BitConverter.ToSingle( this.buffer , this.bufferPos );
                this.bufferPos += 4;
                return ret;
            }

            var rawBytes = this.ReadRawBytes( 4 );
            if ( !BitConverter.IsLittleEndian )
                ByteArray.Reverse( rawBytes );
            return BitConverter.ToSingle( rawBytes , 0 );
        }

        /// <summary>
        ///     Reads a uint64 field from the stream.
        /// </summary>
        public ulong ReadUInt64()
        {
            return this.ReadRawVarint64();
        }

        /// <summary>
        ///     Reads an int64 field from the stream.
        /// </summary>
        public long ReadInt64()
        {
            return ( long ) this.ReadRawVarint64();
        }

        /// <summary>
        ///     Reads an int32 field from the stream.
        /// </summary>
        public int ReadInt32()
        {
            return ( int ) this.ReadRawVarint32();
        }

        /// <summary>
        ///     Reads a fixed64 field from the stream.
        /// </summary>
        public ulong ReadFixed64()
        {
            return this.ReadRawLittleEndian64();
        }

        /// <summary>
        ///     Reads a fixed32 field from the stream.
        /// </summary>
        public uint ReadFixed32()
        {
            return this.ReadRawLittleEndian32();
        }

        /// <summary>
        ///     Reads a bool field from the stream.
        /// </summary>
        public bool ReadBool()
        {
            return this.ReadRawVarint32() != 0;
        }

        /// <summary>
        ///     Reads a string field from the stream.
        /// </summary>
        public string ReadString()
        {
            var length = this.ReadLength();
            // No need to read any data for an empty string.
            if ( length == 0 )
                return "";
            if ( length <= this.bufferSize - this.bufferPos )
            {
                // Fast path:  We already have the bytes in a contiguous buffer, so
                //   just copy directly from it.
                var result = CodedOutputStream.Utf8Encoding.GetString( this.buffer , this.bufferPos , length );
                this.bufferPos += length;
                return result;
            }

            // Slow path: Build a byte array first then copy it.
            return CodedOutputStream.Utf8Encoding.GetString( this.ReadRawBytes( length ) , 0 , length );
        }

        /// <summary>
        ///     Reads an embedded message field value from the stream.
        /// </summary>
        public void ReadMessage ( IMessage builder )
        {
            var length = this.ReadLength();
            if ( this.recursionDepth >= this.RecursionLimit )
                throw InvalidProtocolBufferException.RecursionLimitExceeded();
            var oldLimit = this.PushLimit( length );
            ++this.recursionDepth;
            builder.MergeFrom( this );
            this.CheckReadEndOfStreamTag();
            // Check that we've read exactly as much data as expected.
            if ( !this.ReachedLimit )
                throw InvalidProtocolBufferException.TruncatedMessage();
            --this.recursionDepth;
            this.PopLimit( oldLimit );
        }

        /// <summary>
        ///     Reads a bytes field value from the stream.
        /// </summary>
        public ByteString ReadBytes()
        {
            var length = this.ReadLength();
            if ( length <= this.bufferSize - this.bufferPos && length > 0 )
            {
                // Fast path:  We already have the bytes in a contiguous buffer, so
                //   just copy directly from it.
                var result = ByteString.CopyFrom( this.buffer , this.bufferPos , length );
                this.bufferPos += length;
                return result;
            }

            // Slow path:  Build a byte array and attach it to a new ByteString.
            return ByteString.AttachBytes( this.ReadRawBytes( length ) );
        }

        /// <summary>
        ///     Reads a uint32 field value from the stream.
        /// </summary>
        public uint ReadUInt32()
        {
            return this.ReadRawVarint32();
        }

        /// <summary>
        ///     Reads an enum field value from the stream.
        /// </summary>
        public int ReadEnum()
        {
            // Currently just a pass-through, but it's nice to separate it logically from WriteInt32.
            return ( int ) this.ReadRawVarint32();
        }

        /// <summary>
        ///     Reads an sfixed32 field value from the stream.
        /// </summary>
        public int ReadSFixed32()
        {
            return ( int ) this.ReadRawLittleEndian32();
        }

        /// <summary>
        ///     Reads an sfixed64 field value from the stream.
        /// </summary>
        public long ReadSFixed64()
        {
            return ( long ) this.ReadRawLittleEndian64();
        }

        /// <summary>
        ///     Reads an sint32 field value from the stream.
        /// </summary>
        public int ReadSInt32()
        {
            return DecodeZigZag32( this.ReadRawVarint32() );
        }

        /// <summary>
        ///     Reads an sint64 field value from the stream.
        /// </summary>
        public long ReadSInt64()
        {
            return DecodeZigZag64( this.ReadRawVarint64() );
        }

        /// <summary>
        ///     Reads a length for length-delimited data.
        /// </summary>
        /// <remarks>
        ///     This is internally just reading a varint, but this method exists
        ///     to make the calling code clearer.
        /// </remarks>
        public int ReadLength()
        {
            return ( int ) this.ReadRawVarint32();
        }

        /// <summary>
        ///     Peeks at the next tag in the stream. If it matches <paramref name="tag" />,
        ///     the tag is consumed and the method returns <c>true</c>; otherwise, the
        ///     stream is left in the original position and the method returns <c>false</c>.
        /// </summary>
        public bool MaybeConsumeTag ( uint tag )
        {
            if ( this.PeekTag() == tag )
            {
                this.hasNextTag = false;
                return true;
            }

            return false;
        }

        #endregion

        #region Underlying reading primitives

        /// <summary>
        ///     Same code as ReadRawVarint32, but read each byte individually, checking for
        ///     buffer overflow.
        /// </summary>
        private uint SlowReadRawVarint32()
        {
            int tmp = this.ReadRawByte();
            if ( tmp < 128 )
                return ( uint ) tmp;
            var result = tmp & 0x7f;
            if ( ( tmp = this.ReadRawByte() ) < 128 )
            {
                result |= tmp << 7;
            }
            else
            {
                result |= ( tmp & 0x7f ) << 7;
                if ( ( tmp = this.ReadRawByte() ) < 128 )
                {
                    result |= tmp << 14;
                }
                else
                {
                    result |= ( tmp & 0x7f ) << 14;
                    if ( ( tmp = this.ReadRawByte() ) < 128 )
                    {
                        result |= tmp << 21;
                    }
                    else
                    {
                        result |= ( tmp & 0x7f ) << 21;
                        result |= ( tmp = this.ReadRawByte() ) << 28;
                        if ( tmp >= 128 )
                        {
                            // Discard upper 32 bits.
                            for ( var i = 0; i < 5; i++ )
                                if ( this.ReadRawByte() < 128 )
                                    return ( uint ) result;
                            throw InvalidProtocolBufferException.MalformedVarint();
                        }
                    }
                }
            }

            return ( uint ) result;
        }

        /// <summary>
        ///     Reads a raw Varint from the stream.  If larger than 32 bits, discard the upper bits.
        ///     This method is optimised for the case where we've got lots of data in the buffer.
        ///     That means we can check the size just once, then just read directly from the buffer
        ///     without constant rechecking of the buffer length.
        /// </summary>
        internal uint ReadRawVarint32()
        {
            if ( this.bufferPos + 5 > this.bufferSize )
                return this.SlowReadRawVarint32();

            int tmp = this.buffer [ this.bufferPos++ ];
            if ( tmp < 128 )
                return ( uint ) tmp;
            var result = tmp & 0x7f;
            if ( ( tmp = this.buffer [ this.bufferPos++ ] ) < 128 )
            {
                result |= tmp << 7;
            }
            else
            {
                result |= ( tmp & 0x7f ) << 7;
                if ( ( tmp = this.buffer [ this.bufferPos++ ] ) < 128 )
                {
                    result |= tmp << 14;
                }
                else
                {
                    result |= ( tmp & 0x7f ) << 14;
                    if ( ( tmp = this.buffer [ this.bufferPos++ ] ) < 128 )
                    {
                        result |= tmp << 21;
                    }
                    else
                    {
                        result |= ( tmp & 0x7f ) << 21;
                        result |= ( tmp = this.buffer [ this.bufferPos++ ] ) << 28;
                        if ( tmp >= 128 )
                        {
                            // Discard upper 32 bits.
                            // Note that this has to use ReadRawByte() as we only ensure we've
                            // got at least 5 bytes at the start of the method. This lets us
                            // use the fast path in more cases, and we rarely hit this section of code.
                            for ( var i = 0; i < 5; i++ )
                                if ( this.ReadRawByte() < 128 )
                                    return ( uint ) result;
                            throw InvalidProtocolBufferException.MalformedVarint();
                        }
                    }
                }
            }

            return ( uint ) result;
        }

        /// <summary>
        ///     Reads a varint from the input one byte at a time, so that it does not
        ///     read any bytes after the end of the varint. If you simply wrapped the
        ///     stream in a CodedInputStream and used ReadRawVarint32(Stream)
        ///     then you would probably end up reading past the end of the varint since
        ///     CodedInputStream buffers its input.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static uint ReadRawVarint32 ( Stream input )
        {
            var result = 0;
            var offset = 0;
            for ( ; offset < 32; offset += 7 )
            {
                var b = input.ReadByte();
                if ( b == -1 )
                    throw InvalidProtocolBufferException.TruncatedMessage();
                result |= ( b & 0x7f ) << offset;
                if ( ( b & 0x80 ) == 0 )
                    return ( uint ) result;
            }

            // Keep reading up to 64 bits.
            for ( ; offset < 64; offset += 7 )
            {
                var b = input.ReadByte();
                if ( b == -1 )
                    throw InvalidProtocolBufferException.TruncatedMessage();
                if ( ( b & 0x80 ) == 0 )
                    return ( uint ) result;
            }

            throw InvalidProtocolBufferException.MalformedVarint();
        }

        /// <summary>
        ///     Reads a raw varint from the stream.
        /// </summary>
        internal ulong ReadRawVarint64()
        {
            var shift = 0;
            ulong result = 0;
            while ( shift < 64 )
            {
                var b = this.ReadRawByte();
                result |= ( ulong ) ( b & 0x7F ) << shift;
                if ( ( b & 0x80 ) == 0 )
                    return result;
                shift += 7;
            }

            throw InvalidProtocolBufferException.MalformedVarint();
        }

        /// <summary>
        ///     Reads a 32-bit little-endian integer from the stream.
        /// </summary>
        internal uint ReadRawLittleEndian32()
        {
            uint b1 = this.ReadRawByte();
            uint b2 = this.ReadRawByte();
            uint b3 = this.ReadRawByte();
            uint b4 = this.ReadRawByte();
            return b1 | ( b2 << 8 ) | ( b3 << 16 ) | ( b4 << 24 );
        }

        /// <summary>
        ///     Reads a 64-bit little-endian integer from the stream.
        /// </summary>
        internal ulong ReadRawLittleEndian64()
        {
            ulong b1 = this.ReadRawByte();
            ulong b2 = this.ReadRawByte();
            ulong b3 = this.ReadRawByte();
            ulong b4 = this.ReadRawByte();
            ulong b5 = this.ReadRawByte();
            ulong b6 = this.ReadRawByte();
            ulong b7 = this.ReadRawByte();
            ulong b8 = this.ReadRawByte();
            return b1 | ( b2 << 8 ) | ( b3 << 16 ) | ( b4 << 24 )
                   | ( b5 << 32 ) | ( b6 << 40 ) | ( b7 << 48 ) | ( b8 << 56 );
        }

        /// <summary>
        ///     Decode a 32-bit value with ZigZag encoding.
        /// </summary>
        /// <remarks>
        ///     ZigZag encodes signed integers into values that can be efficiently
        ///     encoded with varint.  (Otherwise, negative values must be
        ///     sign-extended to 64 bits to be varint encoded, thus always taking
        ///     10 bytes on the wire.)
        /// </remarks>
        internal static int DecodeZigZag32 ( uint n )
        {
            return ( int ) ( n >> 1 ) ^ -( int ) ( n & 1 );
        }

        /// <summary>
        ///     Decode a 32-bit value with ZigZag encoding.
        /// </summary>
        /// <remarks>
        ///     ZigZag encodes signed integers into values that can be efficiently
        ///     encoded with varint.  (Otherwise, negative values must be
        ///     sign-extended to 64 bits to be varint encoded, thus always taking
        ///     10 bytes on the wire.)
        /// </remarks>
        internal static long DecodeZigZag64 ( ulong n )
        {
            return ( long ) ( n >> 1 ) ^ -( long ) ( n & 1 );
        }

        #endregion

        #region Internal reading and buffer management

        /// <summary>
        ///     Sets currentLimit to (current position) + byteLimit. This is called
        ///     when descending into a length-delimited embedded message. The previous
        ///     limit is returned.
        /// </summary>
        /// <returns>The old limit.</returns>
        internal int PushLimit ( int byteLimit )
        {
            if ( byteLimit < 0 )
                throw InvalidProtocolBufferException.NegativeSize();
            byteLimit += this.totalBytesRetired + this.bufferPos;
            var oldLimit = this.currentLimit;
            if ( byteLimit > oldLimit )
                throw InvalidProtocolBufferException.TruncatedMessage();
            this.currentLimit = byteLimit;

            this.RecomputeBufferSizeAfterLimit();

            return oldLimit;
        }

        private void RecomputeBufferSizeAfterLimit()
        {
            this.bufferSize += this.bufferSizeAfterLimit;
            var bufferEnd = this.totalBytesRetired + this.bufferSize;
            if ( bufferEnd > this.currentLimit )
            {
                // Limit is in current buffer.
                this.bufferSizeAfterLimit = bufferEnd - this.currentLimit;
                this.bufferSize -= this.bufferSizeAfterLimit;
            }
            else
            {
                this.bufferSizeAfterLimit = 0;
            }
        }

        /// <summary>
        ///     Discards the current limit, returning the previous limit.
        /// </summary>
        internal void PopLimit ( int oldLimit )
        {
            this.currentLimit = oldLimit;
            this.RecomputeBufferSizeAfterLimit();
        }

        /// <summary>
        ///     Returns whether or not all the data before the limit has been read.
        /// </summary>
        /// <returns></returns>
        internal bool ReachedLimit
        {
            get
            {
                if ( this.currentLimit == int.MaxValue )
                    return false;
                var currentAbsolutePosition = this.totalBytesRetired + this.bufferPos;
                return currentAbsolutePosition >= this.currentLimit;
            }
        }

        /// <summary>
        ///     Returns true if the stream has reached the end of the input. This is the
        ///     case if either the end of the underlying input source has been reached or
        ///     the stream has reached a limit created using PushLimit.
        /// </summary>
        public bool IsAtEnd => this.bufferPos == this.bufferSize && !this.RefillBuffer( false );

        /// <summary>
        ///     Called when buffer is empty to read more bytes from the
        ///     input.  If <paramref name="mustSucceed" /> is true, RefillBuffer() gurantees that
        ///     either there will be at least one byte in the buffer when it returns
        ///     or it will throw an exception.  If <paramref name="mustSucceed" /> is false,
        ///     RefillBuffer() returns false if no more bytes were available.
        /// </summary>
        /// <param name="mustSucceed"></param>
        /// <returns></returns>
        private bool RefillBuffer ( bool mustSucceed )
        {
            if ( this.bufferPos < this.bufferSize )
                throw new InvalidOperationException( "RefillBuffer() called when buffer wasn't empty." );

            if ( this.totalBytesRetired + this.bufferSize == this.currentLimit )
            {
                // Oops, we hit a limit.
                if ( mustSucceed )
                    throw InvalidProtocolBufferException.TruncatedMessage();
                return false;
            }

            this.totalBytesRetired += this.bufferSize;

            this.bufferPos = 0;
            this.bufferSize = this.input == null ? 0 : this.input.Read( this.buffer , 0 , this.buffer.Length );
            if ( this.bufferSize < 0 )
                throw new InvalidOperationException( "Stream.Read returned a negative count" );
            if ( this.bufferSize == 0 )
            {
                if ( mustSucceed )
                    throw InvalidProtocolBufferException.TruncatedMessage();
                return false;
            }

            this.RecomputeBufferSizeAfterLimit();
            var totalBytesRead = this.totalBytesRetired + this.bufferSize + this.bufferSizeAfterLimit;
            if ( totalBytesRead < 0 || totalBytesRead > this.SizeLimit )
                throw InvalidProtocolBufferException.SizeLimitExceeded();
            return true;
        }

        /// <summary>
        ///     Read one byte from the input.
        /// </summary>
        /// <exception cref="InvalidProtocolBufferException">
        ///     the end of the stream or the current limit was reached
        /// </exception>
        internal byte ReadRawByte()
        {
            if ( this.bufferPos == this.bufferSize )
                this.RefillBuffer( true );
            return this.buffer [ this.bufferPos++ ];
        }

        /// <summary>
        ///     Reads a fixed size of bytes from the input.
        /// </summary>
        /// <exception cref="InvalidProtocolBufferException">
        ///     the end of the stream or the current limit was reached
        /// </exception>
        internal byte [ ] ReadRawBytes ( int size )
        {
            if ( size < 0 )
                throw InvalidProtocolBufferException.NegativeSize();

            if ( this.totalBytesRetired + this.bufferPos + size > this.currentLimit )
            {
                // Read to the end of the stream (up to the current limit) anyway.
                this.SkipRawBytes( this.currentLimit - this.totalBytesRetired - this.bufferPos );
                // Then fail.
                throw InvalidProtocolBufferException.TruncatedMessage();
            }

            if ( size <= this.bufferSize - this.bufferPos )
            {
                // We have all the bytes we need already.
                var bytes = new byte[ size ];
                ByteArray.Copy( this.buffer , this.bufferPos , bytes , 0 , size );
                this.bufferPos += size;
                return bytes;
            }

            if ( size < this.buffer.Length )
            {
                // Reading more bytes than are in the buffer, but not an excessive number
                // of bytes.  We can safely allocate the resulting array ahead of time.

                // First copy what we have.
                var bytes = new byte[ size ];
                var pos = this.bufferSize - this.bufferPos;
                ByteArray.Copy( this.buffer , this.bufferPos , bytes , 0 , pos );
                this.bufferPos = this.bufferSize;

                // We want to use RefillBuffer() and then copy from the buffer into our
                // byte array rather than reading directly into our byte array because
                // the input may be unbuffered.
                this.RefillBuffer( true );

                while ( size - pos > this.bufferSize )
                {
                    Buffer.BlockCopy( this.buffer , 0 , bytes , pos , this.bufferSize );
                    pos += this.bufferSize;
                    this.bufferPos = this.bufferSize;
                    this.RefillBuffer( true );
                }

                ByteArray.Copy( this.buffer , 0 , bytes , pos , size - pos );
                this.bufferPos = size - pos;

                return bytes;
            }
            else
            {
                // The size is very large.  For security reasons, we can't allocate the
                // entire byte array yet.  The size comes directly from the input, so a
                // maliciously-crafted message could provide a bogus very large size in
                // order to trick the app into allocating a lot of memory.  We avoid this
                // by allocating and reading only a small chunk at a time, so that the
                // malicious message must actually *be* extremely large to cause
                // problems.  Meanwhile, we limit the allowed size of a message elsewhere.

                // Remember the buffer markers since we'll have to copy the bytes out of
                // it later.
                var originalBufferPos = this.bufferPos;
                var originalBufferSize = this.bufferSize;

                // Mark the current buffer consumed.
                this.totalBytesRetired += this.bufferSize;
                this.bufferPos = 0;
                this.bufferSize = 0;

                // Read all the rest of the bytes we need.
                var sizeLeft = size - ( originalBufferSize - originalBufferPos );
                var chunks = new List<byte [ ]>();

                while ( sizeLeft > 0 )
                {
                    var chunk = new byte[ Math.Min( sizeLeft , this.buffer.Length ) ];
                    var pos = 0;
                    while ( pos < chunk.Length )
                    {
                        var n = this.input == null ? -1 : this.input.Read( chunk , pos , chunk.Length - pos );
                        if ( n <= 0 )
                            throw InvalidProtocolBufferException.TruncatedMessage();
                        this.totalBytesRetired += n;
                        pos += n;
                    }

                    sizeLeft -= chunk.Length;
                    chunks.Add( chunk );
                }

                // OK, got everything.  Now concatenate it all into one buffer.
                var bytes = new byte[ size ];

                // Start by copying the leftover bytes from this.buffer.
                var newPos = originalBufferSize - originalBufferPos;
                ByteArray.Copy( this.buffer , originalBufferPos , bytes , 0 , newPos );

                // And now all the chunks.
                foreach ( var chunk in chunks )
                {
                    Buffer.BlockCopy( chunk , 0 , bytes , newPos , chunk.Length );
                    newPos += chunk.Length;
                }

                // Done.
                return bytes;
            }
        }

        /// <summary>
        ///     Reads and discards <paramref name="size" /> bytes.
        /// </summary>
        /// <exception cref="InvalidProtocolBufferException">
        ///     the end of the stream
        ///     or the current limit was reached
        /// </exception>
        private void SkipRawBytes ( int size )
        {
            if ( size < 0 )
                throw InvalidProtocolBufferException.NegativeSize();

            if ( this.totalBytesRetired + this.bufferPos + size > this.currentLimit )
            {
                // Read to the end of the stream anyway.
                this.SkipRawBytes( this.currentLimit - this.totalBytesRetired - this.bufferPos );
                // Then fail.
                throw InvalidProtocolBufferException.TruncatedMessage();
            }

            if ( size <= this.bufferSize - this.bufferPos )
            {
                // We have all the bytes we need already.
                this.bufferPos += size;
            }
            else
            {
                // Skipping more bytes than are in the buffer.  First skip what we have.
                var pos = this.bufferSize - this.bufferPos;

                // ROK 5/7/2013 Issue #54: should retire all bytes in buffer (bufferSize)
                // totalBytesRetired += pos;
                this.totalBytesRetired += this.bufferSize;

                this.bufferPos = 0;
                this.bufferSize = 0;

                // Then skip directly from the InputStream for the rest.
                if ( pos < size )
                {
                    if ( this.input == null )
                        throw InvalidProtocolBufferException.TruncatedMessage();
                    this.SkipImpl( size - pos );
                    this.totalBytesRetired += size - pos;
                }
            }
        }

        /// <summary>
        ///     Abstraction of skipping to cope with streams which can't really skip.
        /// </summary>
        private void SkipImpl ( int amountToSkip )
        {
            if ( this.input.CanSeek )
            {
                var previousPosition = this.input.Position;
                this.input.Position += amountToSkip;
                if ( this.input.Position != previousPosition + amountToSkip )
                    throw InvalidProtocolBufferException.TruncatedMessage();
            }
            else
            {
                var skipBuffer = new byte[ Math.Min( 1024 , amountToSkip ) ];
                while ( amountToSkip > 0 )
                {
                    var bytesRead = this.input.Read( skipBuffer , 0 , Math.Min( skipBuffer.Length , amountToSkip ) );
                    if ( bytesRead <= 0 )
                        throw InvalidProtocolBufferException.TruncatedMessage();
                    amountToSkip -= bytesRead;
                }
            }
        }

        #endregion
    }
}