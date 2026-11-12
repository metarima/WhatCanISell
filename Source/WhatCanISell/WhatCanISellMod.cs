using System.Runtime.Versioning;
using HarmonyLib;
using Verse;

// Built against the game's own .NET 10 assemblies; the SDK target framework is only nominal.
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
