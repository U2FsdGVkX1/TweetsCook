using System;
using System.Collections.Generic;
using System.Text;

namespace TweetsCook.Sources
{
    partial interface ISource
    {
        public abstract void Start();
    }
}
