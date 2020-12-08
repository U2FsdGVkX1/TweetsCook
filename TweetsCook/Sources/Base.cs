using System;
using System.Collections.Generic;
using System.Text;

namespace TweetsCook.Sources
{
    abstract partial class Base
    {
        public readonly Uri SourceUri;
        public Base(string sourceUri)
        {
            SourceUri = new Uri(sourceUri);
        }
        public abstract void Start();
    }
}
