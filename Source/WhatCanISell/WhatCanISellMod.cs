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

        public static WhatCanISellMod Instance { get; private set; }
        public static WhatCanISellSettings Settings { get; private set; }

        public WhatCanISellMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<WhatCanISellSettings>();
            new Harmony("luissousa.whatcanisell").PatchAll();
        }
    }

    public class WhatCanISellSettings : ModSettings
    {
        public const int DefaultLookWithinTiles = 25;
        public const int MaxLookWithinTiles = 500;

        public int lookWithinTiles = DefaultLookWithinTiles;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lookWithinTiles, "lookWithinTiles", DefaultLookWithinTiles);
        }
    }
}
