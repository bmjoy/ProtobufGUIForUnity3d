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
using System.Collections;
using System.Collections.Generic;

namespace Google.Protobuf.Collections
{
    /// <summary>
    ///     Read-only wrapper around another dictionary.
    /// </summary>
    internal sealed class ReadOnlyDictionary<TKey , TValue> : IDictionary<TKey , TValue>
    {
        private readonly IDictionary<TKey , TValue> wrapped;

        public ReadOnlyDictionary ( IDictionary<TKey , TValue> wrapped )
        {
            this.wrapped = wrapped;
        }

        public void Add ( TKey key , TValue value )
        {
            throw new InvalidOperationException();
        }

        public bool ContainsKey ( TKey key )
        {
            return this.wrapped.ContainsKey( key );
        }

        public ICollection<TKey> Keys => this.wrapped.Keys;

        public bool Remove ( TKey key )
        {
            throw new InvalidOperationException();
        }

        public bool TryGetValue ( TKey key , out TValue value )
        {
            return this.wrapped.TryGetValue( key , out value );
        }

        public ICollection<TValue> Values => this.wrapped.Values;

        public TValue this [ TKey key ]
        {
            get { return this.wrapped [ key ]; }
            set { throw new InvalidOperationException(); }
        }

        public void Add ( KeyValuePair<TKey , TValue> item )
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains ( KeyValuePair<TKey , TValue> item )
        {
            return this.wrapped.Contains( item );
        }

        public void CopyTo ( KeyValuePair<TKey , TValue> [ ] array , int arrayIndex )
        {
            this.wrapped.CopyTo( array , arrayIndex );
        }

        public int Count => this.wrapped.Count;

        public bool IsReadOnly => true;

        public bool Remove ( KeyValuePair<TKey , TValue> item )
        {
            throw new InvalidOperationException();
        }

        public IEnumerator<KeyValuePair<TKey , TValue>> GetEnumerator()
        {
            return this.wrapped.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ( ( IEnumerable ) this.wrapped ).GetEnumerator();
        }

        public override bool Equals ( object obj )
        {
            return this.wrapped.Equals( obj );
        }

        public override int GetHashCode()
        {
            return this.wrapped.GetHashCode();
        }

        public override string ToString()
        {
            return this.wrapped.ToString();
        }
    }
}