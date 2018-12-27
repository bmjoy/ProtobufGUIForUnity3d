using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Google.Protobuf.Examples.AddressBook
{
    public class Main : MonoBehaviour
    {
        private void Start()
        {
            var person = new Person();
            person.Id = 2;
            person.Phones.Add( new List<Person.Types.PhoneNumber>
            {
                new Person.Types.PhoneNumber {Type = Person.Types.PhoneType.Home , Number = "123312"}
            } );
            byte [ ] bytes;
            using ( var ms = new MemoryStream() )
            {
                person.WriteTo( ms );
                bytes = ms.ToArray();
            }

            person = Person.Parser.ParseFrom( bytes );

            Debug.Log( $"{person.Id}" );
        }
    }
}