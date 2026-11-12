using System.Runtime.Versioning;
using HarmonyLib;
using Verse;

// RimWorld 1.7 mods target .NET 10; we compile against the game's own framework assemblies.
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]

namespace WhatCanISell
{
    public class WhatCanISellMod : Mod
    {
        public const string LogPrefix = "[What Can I Sell?] ";

        public WhatCanISellMod(ModContentPack content) : base(content)
        {
            new Harmony("luissousa.whatcanisell").PatchAll();
        }
    }
}
