﻿using System;
using System.Collections.Generic;
using wavynet.vm.core;

namespace wavynet.vm.data.items
{
    [Serializable]
    public class WavyFunction : WavyItem
    {
        public string name;
        // Used for native functions, if native, we need to be able to resolve the dll file
        public bool is_native;
        public int args_size;
        public int locals_size;
        public Int32[] bytecode;

        public WavyFunction(string name, bool is_native, int args_size, int locals_size, Int32[] bytecode) : base(ItemType.FUNC)
        {
            this.name = name;
            this.is_native = is_native;
            this.args_size = args_size;
            this.locals_size = locals_size;
            this.bytecode = bytecode;
        }

        public WavyFunction()
        {

        }

        public WavyItem call(List<WavyItem> args)
        {
            return null;
        }
    }
}