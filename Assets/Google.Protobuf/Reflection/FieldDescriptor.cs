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

namespace Google.Protobuf.Reflection
{
    /// <summary>
    ///     Descriptor for a field or extension within a message in a .proto file.
    /// </summary>
    public sealed class FieldDescriptor : DescriptorBase , IComparable<FieldDescriptor>
    {
        private readonly string propertyName; // Annoyingly, needed in Crosslink.
        private EnumDescriptor enumType;
        private MessageDescriptor messageType;

        internal FieldDescriptor ( FieldDescriptorProto proto , FileDescriptor file ,
            MessageDescriptor parent , int index , string propertyName )
            : base( file , file.ComputeFullName( parent , proto.Name ) , index )
        {
            this.Proto = proto;
            if ( proto.Type != 0 )
                this.FieldType = GetFieldTypeFromProtoType( proto.Type );

            if ( this.FieldNumber <= 0 )
                throw new DescriptorValidationException( this , "Field numbers must be positive integers." );
            this.ContainingType = parent;
            // OneofIndex "defaults" to -1 due to a hack in FieldDescriptor.OnConstruction.
            if ( proto.OneofIndex != -1 )
            {
                if ( proto.OneofIndex < 0 || proto.OneofIndex >= parent.Proto.OneofDecl.Count )
                    throw new DescriptorValidationException( this ,
                        $"FieldDescriptorProto.oneof_index is out of range for type {parent.Name}" );
                this.ContainingOneof = parent.Oneofs [ proto.OneofIndex ];
            }

            file.DescriptorPool.AddSymbol( this );
            // We can't create the accessor until we've cross-linked, unfortunately, as we
            // may not know whether the type of the field is a map or not. Remember the property name
            // for later.
            // We could trust the generated code and check whether the type of the property is
            // a MapField, but that feels a tad nasty.
            this.propertyName = propertyName;
            this.JsonName = this.Proto.JsonName == "" ? JsonFormatter.ToJsonName( this.Proto.Name ) : this.Proto.JsonName;
        }

        /// <summary>
        ///     Get the field's containing message type.
        /// </summary>
        public MessageDescriptor ContainingType { get; }

        /// <summary>
        ///     Returns the oneof containing this field, or <c>null</c> if it is not part of a oneof.
        /// </summary>
        public OneofDescriptor ContainingOneof { get; }

        /// <summary>
        ///     The effective JSON name for this field. This is usually the lower-camel-cased form of the field name,
        ///     but can be overridden using the <c>json_name</c> option in the .proto file.
        /// </summary>
        public string JsonName { get; }

        internal FieldDescriptorProto Proto { get; }


        /// <summary>
        ///     The brief name of the descriptor's target.
        /// </summary>
        public override string Name => this.Proto.Name;

        /// <summary>
        ///     Returns the accessor for this field.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         While a <see cref="FieldDescriptor" /> describes the field, it does not provide
        ///         any way of obtaining or changing the value of the field within a specific message;
        ///         that is the responsibility of the accessor.
        ///     </para>
        ///     <para>
        ///         The value returned by this property will be non-null for all regular fields. However,
        ///         if a message containing a map field is introspected, the list of nested messages will include
        ///         an auto-generated nested key/value pair message for the field. This is not represented in any
        ///         generated type, and the value of the map field itself is represented by a dictionary in the
        ///         reflection API. There are never instances of those "hidden" messages, so no accessor is provided
        ///         and this property will return null.
        ///     </para>
        /// </remarks>
        public IFieldAccessor Accessor { get; private set; }

        /// <summary>
        ///     Returns <c>true</c> if this field is a repeated field; <c>false</c> otherwise.
        /// </summary>
        public bool IsRepeated => this.Proto.Label == FieldDescriptorProto.Types.Label.Repeated;

        /// <summary>
        ///     Returns <c>true</c> if this field is a map field; <c>false</c> otherwise.
        /// </summary>
        public bool IsMap => this.FieldType == FieldType.Message && this.messageType.Proto.Options != null && this.messageType.Proto.Options.MapEntry;

        /// <summary>
        ///     Returns <c>true</c> if this field is a packed, repeated field; <c>false</c> otherwise.
        /// </summary>
        public bool IsPacked =>
            // Note the || rather than && here - we're effectively defaulting to packed, because that *is*
            // the default in proto3, which is all we support. We may give the wrong result for the protos
            // within descriptor.proto, but that's okay, as they're never exposed and we don't use IsPacked
            // within the runtime.
            this.Proto.Options == null || this.Proto.Options.Packed;

        /// <summary>
        ///     Returns the type of the field.
        /// </summary>
        public FieldType FieldType { get; private set; }

        /// <summary>
        ///     Returns the field number declared in the proto file.
        /// </summary>
        public int FieldNumber => this.Proto.Number;

        /// <summary>
        ///     For enum fields, returns the field's type.
        /// </summary>
        public EnumDescriptor EnumType
        {
            get
            {
                if ( this.FieldType != FieldType.Enum )
                    throw new InvalidOperationException( "EnumType is only valid for enum fields." );
                return this.enumType;
            }
        }

        /// <summary>
        ///     For embedded message and group fields, returns the field's type.
        /// </summary>
        public MessageDescriptor MessageType
        {
            get
            {
                if ( this.FieldType != FieldType.Message )
                    throw new InvalidOperationException( "MessageType is only valid for message fields." );
                return this.messageType;
            }
        }

        /// <summary>
        ///     The (possibly empty) set of custom options for this field.
        /// </summary>
        public CustomOptions CustomOptions => this.Proto.Options?.CustomOptions ?? CustomOptions.Empty;

        /// <summary>
        ///     Compares this descriptor with another one, ordering in "canonical" order
        ///     which simply means ascending order by field number. <paramref name="other" />
        ///     must be a field of the same type, i.e. the <see cref="ContainingType" /> of
        ///     both fields must be the same.
        /// </summary>
        public int CompareTo ( FieldDescriptor other )
        {
            if ( other.ContainingType != this.ContainingType )
                throw new ArgumentException( "FieldDescriptors can only be compared to other FieldDescriptors " +
                                             "for fields of the same message type." );
            return this.FieldNumber - other.FieldNumber;
        }

        /// <summary>
        ///     Maps a field type as included in the .proto file to a FieldType.
        /// </summary>
        private static FieldType GetFieldTypeFromProtoType ( FieldDescriptorProto.Types.Type type )
        {
            switch ( type )
            {
                case FieldDescriptorProto.Types.Type.Double :
                    return FieldType.Double;
                case FieldDescriptorProto.Types.Type.Float :
                    return FieldType.Float;
                case FieldDescriptorProto.Types.Type.Int64 :
                    return FieldType.Int64;
                case FieldDescriptorProto.Types.Type.Uint64 :
                    return FieldType.UInt64;
                case FieldDescriptorProto.Types.Type.Int32 :
                    return FieldType.Int32;
                case FieldDescriptorProto.Types.Type.Fixed64 :
                    return FieldType.Fixed64;
                case FieldDescriptorProto.Types.Type.Fixed32 :
                    return FieldType.Fixed32;
                case FieldDescriptorProto.Types.Type.Bool :
                    return FieldType.Bool;
                case FieldDescriptorProto.Types.Type.String :
                    return FieldType.String;
                case FieldDescriptorProto.Types.Type.Group :
                    return FieldType.Group;
                case FieldDescriptorProto.Types.Type.Message :
                    return FieldType.Message;
                case FieldDescriptorProto.Types.Type.Bytes :
                    return FieldType.Bytes;
                case FieldDescriptorProto.Types.Type.Uint32 :
                    return FieldType.UInt32;
                case FieldDescriptorProto.Types.Type.Enum :
                    return FieldType.Enum;
                case FieldDescriptorProto.Types.Type.Sfixed32 :
                    return FieldType.SFixed32;
                case FieldDescriptorProto.Types.Type.Sfixed64 :
                    return FieldType.SFixed64;
                case FieldDescriptorProto.Types.Type.Sint32 :
                    return FieldType.SInt32;
                case FieldDescriptorProto.Types.Type.Sint64 :
                    return FieldType.SInt64;
                default :
                    throw new ArgumentException( "Invalid type specified" );
            }
        }

        /// <summary>
        ///     Look up and cross-link all field types etc.
        /// </summary>
        internal void CrossLink()
        {
            if ( this.Proto.TypeName != "" )
            {
                var typeDescriptor = this.File.DescriptorPool.LookupSymbol( this.Proto.TypeName , this );

                if ( this.Proto.Type != 0 )
                {
                    // Choose field type based on symbol.
                    if ( typeDescriptor is MessageDescriptor )
                        this.FieldType = FieldType.Message;
                    else if ( typeDescriptor is EnumDescriptor )
                        this.FieldType = FieldType.Enum;
                    else
                        throw new DescriptorValidationException( this , $"\"{this.Proto.TypeName}\" is not a type." );
                }

                if ( this.FieldType == FieldType.Message )
                {
                    if ( !( typeDescriptor is MessageDescriptor ) )
                        throw new DescriptorValidationException( this , $"\"{this.Proto.TypeName}\" is not a message type." );
                    this.messageType = ( MessageDescriptor ) typeDescriptor;

                    if ( this.Proto.DefaultValue != "" )
                        throw new DescriptorValidationException( this , "Messages can't have default values." );
                }
                else if ( this.FieldType == FieldType.Enum )
                {
                    if ( !( typeDescriptor is EnumDescriptor ) )
                        throw new DescriptorValidationException( this , $"\"{this.Proto.TypeName}\" is not an enum type." );
                    this.enumType = ( EnumDescriptor ) typeDescriptor;
                }
                else
                {
                    throw new DescriptorValidationException( this , "Field with primitive type has type_name." );
                }
            }
            else
            {
                if ( this.FieldType == FieldType.Message || this.FieldType == FieldType.Enum )
                    throw new DescriptorValidationException( this , "Field with message or enum type missing type_name." );
            }

            // Note: no attempt to perform any default value parsing

            this.File.DescriptorPool.AddFieldByNumber( this );

            if ( this.ContainingType != null && this.ContainingType.Proto.Options != null && this.ContainingType.Proto.Options.MessageSetWireFormat )
                throw new DescriptorValidationException( this , "MessageSet format is not supported." );
            this.Accessor = this.CreateAccessor();
        }

        private IFieldAccessor CreateAccessor()
        {
            // If we're given no property name, that's because we really don't want an accessor.
            // (At the moment, that means it's a map entry message...)
            if ( this.propertyName == null )
                return null;
            var property = this.ContainingType.ClrType.GetProperty( this.propertyName );
            if ( property == null )
                throw new DescriptorValidationException( this , $"Property {this.propertyName} not found in {this.ContainingType.ClrType}" );
            return this.IsMap ? new MapFieldAccessor( property , this )
                : this.IsRepeated ? new RepeatedFieldAccessor( property , this )
                : ( IFieldAccessor ) new SingleFieldAccessor( property , this );
        }
    }
}