using System;
using System.Collections.ObjectModel;

namespace Hzexe.FastDownloader
{
    /// <summary>
    /// A download to Memery job configure 
    /// </summary>
    public class Setup
    {
        public ReadOnlyCollection<string> Mirrors { get; set; }

        public int MaxConnectionPerHost { get; set; } = 8;


       // public event even

        //public Memory  System.IO.MemoryStream Target { get; set; }
    }
}
