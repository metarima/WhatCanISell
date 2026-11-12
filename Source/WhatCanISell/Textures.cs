using UnityEngine;
using Verse;

namespace WhatCanISell
{
    [StaticConstructorOnStartup]
    public static class Textures
    {
        public static readonly Texture2D ThingsYouCanSell = ContentFinder<Texture2D>.Get("UI/Commands/WCIS_ThingsYouCanSell");
    }
}
