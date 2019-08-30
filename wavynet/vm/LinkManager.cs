﻿/*
 * James Clarke
 * 29/08/19
 */

using System.Collections.Generic;
using System.Reflection;

namespace wavynet.vm
{
    /*
     * Handles all linkage functionality for the vm
     */
    public class LinkManager
    {
        // Store assemblies
        public Dictionary<string, Assembly> assemblies;

        public LinkManager()
        {
            this.assemblies = new Dictionary<string, Assembly>();
        }

        // Bind all dlls
        public void bind_all_dll()
        {
            foreach (KeyValuePair<string, string> dll in this.get_all_dlls())
                bind_dll(dll.Key, dll.Value);
        }

        // This gets every dll file within the "native/dll/" installation folder
        private Dictionary<string, string> get_all_dlls()
        {
            return new Dictionary<string, string>() {
                { "TestFile", @"F:\OneDrive - Lancaster University\programming\c#\DLLTest\bin\Debug\netstandard2.0\DLLTest.dll" },
            };
        }

        // Bind the DLL for the native code that we want to use
        private void bind_dll(string name, string path)
        {
            this.assemblies.Add(name, Assembly.LoadFile(path));
        }
    }
}
