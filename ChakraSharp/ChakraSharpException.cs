using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChakraSharp
{
    public class ChakraSharpException : Exception
    {
        public ChakraSharpException()
        {
        }

        public ChakraSharpException(string message)
        : base(message)
        {
        }

        public ChakraSharpException(string message, Exception inner)
        : base(message, inner)
        {
        }
    }
}
