﻿using AnotherClassLibrary;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace DotNet462Application
{
    internal class RepackDropAttribute : Attribute
    {
    }

    internal class AnotherDropAttribute : Attribute
    {
    }

    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine(ImmutableHashSet.Create(1).First());

            int number = new BclAsyncUsage().GetNumber().Result;
            Console.WriteLine(number);

            // This app doesn't have any .xaml files, to reproduce the case when
            // the target library has
            WpfWindowStarter.ShowWindowWithControl();
        }

        [RepackDrop]
        public static void MethodToDrop()
        {
        }

        [AnotherDrop]
        public static void AnotherMethodToDrop()
        {
        }
    }
}
