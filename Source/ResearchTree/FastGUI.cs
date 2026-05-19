using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

internal static class FastGUI
{
    private static readonly RuntimePlatform CurrentPlatform = UnityData.platform;

    private static readonly Type UnityInternalDrawTextureArgumentsType =
        AccessTools.TypeByName("UnityEngine.Internal_DrawTextureArguments");

    private static readonly MethodInfo UnityInternalDrawTextureMethod =
        AccessTools.Method(typeof(Graphics), "Internal_DrawTexture");

    private static readonly Func<object> UnityInternalDrawTextureArgumentsFactory =
        createArgumentsFactory(UnityInternalDrawTextureArgumentsType);

    private static readonly FieldInfo ScreenRectField = UnityInternalDrawTextureArgumentsType?.GetField("screenRect");
    private static readonly FieldInfo TextureField = UnityInternalDrawTextureArgumentsType?.GetField("texture");
    private static readonly FieldInfo ColorField = UnityInternalDrawTextureArgumentsType?.GetField("color");
    private static readonly FieldInfo SourceRectField = UnityInternalDrawTextureArgumentsType?.GetField("sourceRect");
    private static readonly FieldInfo MatField = UnityInternalDrawTextureArgumentsType?.GetField("mat");
    private static readonly FieldInfo BorderWidthsField =
        UnityInternalDrawTextureArgumentsType?.GetField("borderWidths");
    private static readonly FieldInfo CornerRadiusesField =
        UnityInternalDrawTextureArgumentsType?.GetField("cornerRadiuses");
    private static readonly FieldInfo SmoothCornersField =
        UnityInternalDrawTextureArgumentsType?.GetField("smoothCorners");

    private static Func<object> createArgumentsFactory(Type argumentsType)
    {
        if (argumentsType == null)
        {
            return null;
        }

        var newExpression = Expression.New(argumentsType);
        var boxedExpression = Expression.Convert(newExpression, typeof(object));
        return Expression.Lambda<Func<object>>(boxedExpression).Compile();
    }

    private static object createInternalDrawTextureArguments(Rect position, Texture image, Rect? sourceRect,
        Color color)
    {
        if (UnityInternalDrawTextureArgumentsFactory == null)
        {
            throw new InvalidOperationException("Unable to access Unity's internal types.");
        }

        var unityDrawArgs = UnityInternalDrawTextureArgumentsFactory();

        ScreenRectField?.SetValue(unityDrawArgs, position);
        TextureField?.SetValue(unityDrawArgs, image);
        ColorField?.SetValue(unityDrawArgs, color);
        SourceRectField?.SetValue(unityDrawArgs, sourceRect ?? new Rect(0f, 0f, 1f, 1f));
        MatField?.SetValue(unityDrawArgs, Assets.RoundedRectMaterial);
        BorderWidthsField?.SetValue(unityDrawArgs, Vector4.zero);
        CornerRadiusesField?.SetValue(unityDrawArgs, Vector4.zero);
        SmoothCornersField?.SetValue(unityDrawArgs, false);

        return unityDrawArgs;
    }

    public static void DrawTextureFast(Rect position, Texture image, Color color = new())
    {
        if (color == new Color())
        {
            color = GUI.color;
        }

        if (CurrentPlatform == RuntimePlatform.LinuxPlayer &&
            FluffyResearchTreeMod.instance?.Settings?.LinuxUseCompatibilityDrawing == true)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(position, image, ScaleMode.StretchToFill);
            GUI.color = previousColor;
            return;
        }

        if (UnityInternalDrawTextureMethod == null)
        {
            throw new InvalidOperationException("Unable to access Unity's internal methods.");
        }

        var unityDrawArgs = createInternalDrawTextureArguments(position, image, null, color);
        UnityInternalDrawTextureMethod.Invoke(null, [unityDrawArgs]);
    }

    public static void DrawTextureFastWithCoords(Rect position, Texture image, Rect rect, Color color = new())
    {
        if (color == new Color())
        {
            color = GUI.color;
        }

        if (CurrentPlatform == RuntimePlatform.LinuxPlayer &&
            FluffyResearchTreeMod.instance?.Settings?.LinuxUseCompatibilityDrawing == true)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(position, image, rect);
            GUI.color = previousColor;
            return;
        }

        if (UnityInternalDrawTextureMethod == null)
        {
            throw new InvalidOperationException("Unable to access Unity's internal methods.");
        }

        var unityDrawArgs = createInternalDrawTextureArguments(position, image, rect, color);
        UnityInternalDrawTextureMethod.Invoke(null, [unityDrawArgs]);
    }
}
