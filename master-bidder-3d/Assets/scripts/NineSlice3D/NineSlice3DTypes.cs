using System;
using UnityEngine;

namespace NineSlice3D
{
    public enum PivotAnchor
    {
        PreserveOriginalPivot,
        Center,
        BottomCenter,
        TopCenter,
        MinCorner,
        BackCenter,
        FrontCenter
    }

    public enum MeasurementUnit
    {
        Meters,
        Centimeters,
        Millimeters,
        Inches
    }

    [System.Serializable]
    public struct SliceAxisSettings
    {
        [Tooltip("Enable 9-slicing along this axis. If false, this axis is scaled uniformly.")]
        public bool enabled;

        [Tooltip("Margin from the minimum bound (Left, Bottom, or Back). In original local units.")]
        [Min(0f)]
        public float minMargin;

        [Tooltip("Margin from the maximum bound (Right, Top, or Front). In original local units.")]
        [Min(0f)]
        public float maxMargin;

        public SliceAxisSettings(bool enabled, float minMargin, float maxMargin)
        {
            this.enabled = enabled;
            this.minMargin = Mathf.Max(0f, minMargin);
            this.maxMargin = Mathf.Max(0f, maxMargin);
        }
    }

    [System.Serializable]
    public struct SliceBorder3D
    {
        public SliceAxisSettings x; // Left (min), Right (max)
        public SliceAxisSettings y; // Bottom (min), Top (max)
        public SliceAxisSettings z; // Back (min), Front (max)

        public static SliceBorder3D Default => new SliceBorder3D
        {
            x = new SliceAxisSettings(true, 0.1f, 0.1f),
            y = new SliceAxisSettings(true, 0.1f, 0.1f),
            z = new SliceAxisSettings(false, 0f, 0f)
        };
    }

    [System.Serializable]
    public class SubmeshTilingConfig
    {
        [Tooltip("Human-readable label or description (e.g. 'Canvas Sides', 'Body Picture').")]
        public string label = "Submesh Material";

        [Tooltip("Material slot index on the MeshRenderer (0, 1, 2, ...).")]
        [Min(0)]
        public int materialSlotIndex = 0;

        [Tooltip("Enable automatic texture tiling scaling to maintain constant texel density as mesh stretches.")]
        public bool autoScaleTiling = true;

        [Tooltip("Scale tiling along X axis (Width).")]
        public bool scaleTilingX = false;

        [Tooltip("Scale tiling along Y axis (Height).")]
        public bool scaleTilingY = true;

        [Tooltip("Calibration reference size (in cm) where texture looks 1:1 (default 50x50 cm).")]
        public Vector2 referenceSizeCm = new Vector2(50f, 50f);

        [Tooltip("Base texture tiling at reference calibration size.")]
        public Vector2 baseTiling = Vector2.one;

        [Tooltip("Texture offset.")]
        public Vector2 baseOffset = Vector2.zero;

        [Tooltip("Texture property names to update (e.g. _MainTex, _BaseMap, _BumpMap).")]
        public string[] texturePropertyNames = new string[] { "_MainTex", "_BaseMap", "_BumpMap" };

        public SubmeshTilingConfig()
        {
            label = "Sides";
            materialSlotIndex = 1;
            autoScaleTiling = true;
            scaleTilingX = false;
            scaleTilingY = true;
            referenceSizeCm = new Vector2(50f, 50f);
            baseTiling = Vector2.one;
            baseOffset = Vector2.zero;
            texturePropertyNames = new string[] { "_MainTex", "_BaseMap", "_BumpMap" };
        }

        public SubmeshTilingConfig(int slot, string name, bool scaleX, bool scaleY, Vector2 refSizeCm)
        {
            materialSlotIndex = slot;
            label = name;
            autoScaleTiling = true;
            scaleTilingX = scaleX;
            scaleTilingY = scaleY;
            referenceSizeCm = refSizeCm;
            baseTiling = Vector2.one;
            baseOffset = Vector2.zero;
            texturePropertyNames = new string[] { "_MainTex", "_BaseMap", "_BumpMap" };
        }
    }
}
