// Def_Extensions.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public static class Def_Extensions
{
    private static readonly Dictionary<Def, Texture2D> _cachedDefIcons = [];

    private static readonly Dictionary<Def, Color> _cachedIconColors = [];

    extension(Def def)
    {
        public void DrawColouredIcon(Rect canvas)
        {
            GUI.color = def.iconColor();
            GUI.DrawTexture(canvas, def.IconTexture(), ScaleMode.ScaleToFit);
            GUI.color = Color.white;
        }

        private Color iconColor()
        {
            if (def == null)
            {
                return Color.cyan;
            }

            if (_cachedIconColors.TryGetValue(def, out var color))
            {
                return color;
            }

            var buildableDef = def as BuildableDef;
            var thingDef = def as ThingDef;
            var pawnKindDef = def as PawnKindDef;
            if (def is RecipeDef recipeDef && !recipeDef.products.NullOrEmpty())
            {
                _cachedIconColors.Add(def, recipeDef.products.First().thingDef.iconColor());
                return _cachedIconColors[def];
            }

            if (pawnKindDef != null)
            {
                _cachedIconColors.Add(def, pawnKindDef.lifeStages.Last().bodyGraphicData.color);
                return _cachedIconColors[def];
            }

            if (buildableDef == null)
            {
                _cachedIconColors.Add(def, Color.white);
                return _cachedIconColors[def];
            }

            if (thingDef is { entityDefToBuild: not null })
            {
                _cachedIconColors.Add(def, thingDef.entityDefToBuild.iconColor());
                return _cachedIconColors[def];
            }

            if (buildableDef.graphic != null)
            {
                _cachedIconColors.Add(def, buildableDef.graphic.color);
                return _cachedIconColors[def];
            }

            if (thingDef is { MadeFromStuff: true })
            {
                var thingDef2 = GenStuff.DefaultStuffFor(thingDef);
                _cachedIconColors.Add(def, thingDef2.stuffProps.color);
                return _cachedIconColors[def];
            }

            _cachedIconColors.Add(def, Color.white);
            return _cachedIconColors[def];
        }

        public Texture2D IconTexture()
        {
            if (def == null)
            {
                return null;
            }

            if (_cachedDefIcons.TryGetValue(def, out var texture))
            {
                return texture;
            }

            var buildableDef = def as BuildableDef;
            var thingDef = def as ThingDef;
            var pawnKindDef = def as PawnKindDef;
            if (def is RecipeDef recipeDef && !recipeDef.products.NullOrEmpty())
            {
                _cachedDefIcons.Add(def, recipeDef.products.First().thingDef.IconTexture());
                return _cachedDefIcons[def];
            }

            if (pawnKindDef != null)
            {
                try
                {
                    _cachedDefIcons.Add(def,
                        pawnKindDef.lifeStages.Last().bodyGraphicData.Graphic.MatSouth.mainTexture as Texture2D);
                    return _cachedDefIcons[def];
                }
                catch
                {
                    // ignored
                }
            }

            if (buildableDef != null)
            {
                if (thingDef?.entityDefToBuild != null)
                {
                    _cachedDefIcons.Add(def, thingDef.entityDefToBuild.IconTexture());
                    return _cachedDefIcons[def];
                }

                _cachedDefIcons.Add(def, buildableDef.uiIcon);
                return buildableDef.uiIcon;
            }

            _cachedDefIcons.Add(def, null);
            return null;
        }
    }
}