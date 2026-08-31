using UnityEngine;

namespace NineSlice3D
{
    public static class Mesh3DSlicer
    {
        /// <summary>
        /// Resolves a PivotAnchor enum and custom normalized pivot vector into normalized (0..1) coordinates across X, Y, Z.
        /// </summary>
        public static Vector3 GetNormalizedPivot(PivotAnchor pivotAnchor, Vector3 customPivot, Bounds origBounds)
        {
            switch (pivotAnchor)
            {
                case PivotAnchor.Center:
                    return new Vector3(0.5f, 0.5f, 0.5f);

                case PivotAnchor.TopLeft:
                    return new Vector3(0f, 1f, 0.5f);

                case PivotAnchor.TopCenter:
                    return new Vector3(0.5f, 1f, 0.5f);

                case PivotAnchor.TopRight:
                    return new Vector3(1f, 1f, 0.5f);

                case PivotAnchor.MiddleLeft:
                    return new Vector3(0f, 0.5f, 0.5f);

                case PivotAnchor.MiddleRight:
                    return new Vector3(1f, 0.5f, 0.5f);

                case PivotAnchor.BottomLeft:
                    return new Vector3(0f, 0f, 0.5f);

                case PivotAnchor.BottomCenter:
                    return new Vector3(0.5f, 0f, 0.5f);

                case PivotAnchor.BottomRight:
                    return new Vector3(1f, 0f, 0.5f);

                case PivotAnchor.BackCenter:
                    return new Vector3(0.5f, 0.5f, 0f);

                case PivotAnchor.FrontCenter:
                    return new Vector3(0.5f, 0.5f, 1f);

                case PivotAnchor.Custom:
                    return customPivot;

                case PivotAnchor.PreserveOriginalPivot:
                default:
                    Vector3 size = origBounds.size;
                    float px = size.x > 0.0001f ? Mathf.Clamp01((0f - origBounds.min.x) / size.x) : 0.5f;
                    float py = size.y > 0.0001f ? Mathf.Clamp01((0f - origBounds.min.y) / size.y) : 0.5f;
                    float pz = size.z > 0.0001f ? Mathf.Clamp01((0f - origBounds.min.z) / size.z) : 0.5f;
                    return new Vector3(px, py, pz);
            }
        }

        /// <summary>
        /// Calculates target min/max bounds in root space based on original bounds, target size, and pivot anchor.
        /// </summary>
        public static void CalculateTargetBounds(
            Bounds origBounds,
            Vector3 targetSize,
            PivotAnchor pivotAnchor,
            Vector3 customPivot,
            SliceBorder3D borders,
            out Vector3 targetMin,
            out Vector3 targetMax)
        {
            targetSize = new Vector3(
                Mathf.Max(0.0001f, targetSize.x),
                Mathf.Max(0.0001f, targetSize.y),
                Mathf.Max(0.0001f, targetSize.z)
            );

            if (pivotAnchor == PivotAnchor.PreserveOriginalPivot)
            {
                targetMin = new Vector3(
                    CalculateAxisPivotOffset(origBounds.min.x, origBounds.max.x, borders.x, targetSize.x),
                    CalculateAxisPivotOffset(origBounds.min.y, origBounds.max.y, borders.y, targetSize.y),
                    CalculateAxisPivotOffset(origBounds.min.z, origBounds.max.z, borders.z, targetSize.z)
                );
                targetMax = targetMin + targetSize;
                return;
            }

            Vector3 normPivot = GetNormalizedPivot(pivotAnchor, customPivot, origBounds);
            targetMin = new Vector3(
                -targetSize.x * normPivot.x,
                -targetSize.y * normPivot.y,
                -targetSize.z * normPivot.z
            );
            targetMax = targetMin + targetSize;
        }

        public static void CalculateTargetBounds(
            Bounds origBounds,
            Vector3 targetSize,
            PivotAnchor pivotAnchor,
            SliceBorder3D borders,
            out Vector3 targetMin,
            out Vector3 targetMax)
        {
            CalculateTargetBounds(origBounds, targetSize, pivotAnchor, new Vector3(0.5f, 0.5f, 0.5f), borders, out targetMin, out targetMax);
        }

        private static float CalculateAxisPivotOffset(float origMin, float origMax, SliceAxisSettings axis, float targetSize)
        {
            float origSize = origMax - origMin;
            if (origSize <= 0.00001f) return 0f;

            if (!axis.enabled)
            {
                float t = (0f - origMin) / origSize;
                return -t * targetSize;
            }

            float borderMin = origMin + axis.minMargin;
            float borderMax = origMax - axis.maxMargin;
            float L = Mathf.Max(0f, axis.minMargin);
            float R = Mathf.Max(0f, axis.maxMargin);
            float targetCenter = Mathf.Max(0f, targetSize - L - R);

            if (0f <= borderMin)
            {
                return origMin;
            }
            else if (0f >= borderMax)
            {
                return origMax - targetSize;
            }
            else
            {
                float centerOrig = Mathf.Max(0.00001f, borderMax - borderMin);
                float t = (0f - borderMin) / centerOrig;
                return -(L + t * targetCenter);
            }
        }

        /// <summary>
        /// Deforms a single coordinate along one axis using 9-slice rules.
        /// </summary>
        public static float DeformAxisCoordinate(
            float v0,
            float origMin,
            float origMax,
            SliceAxisSettings axis,
            float targetMin,
            float targetMax)
        {
            float origSize = origMax - origMin;
            if (origSize <= 0.00001f) return targetMin;

            float targetSize = targetMax - targetMin;

            if (!axis.enabled)
            {
                // Standard linear stretch
                float t = (v0 - origMin) / origSize;
                return targetMin + t * targetSize;
            }

            float borderMin = origMin + axis.minMargin;
            float borderMax = origMax - axis.maxMargin;
            float L = Mathf.Max(0f, axis.minMargin);
            float R = Mathf.Max(0f, axis.maxMargin);
            float origCenter = Mathf.Max(0f, borderMax - borderMin);

            if (L + R <= 0.00001f || origCenter <= 0.00001f)
            {
                // No sliceable center defined or full corner coverage
                float t = (v0 - origMin) / origSize;
                return targetMin + t * targetSize;
            }

            float scaleL = 1f;
            float scaleR = 1f;
            float targetCenter = targetSize - L - R;

            if (targetCenter < 0f)
            {
                // Target is smaller than corner margins: scale corners down gracefully
                float scale = (L + R > 0.00001f) ? (targetSize / (L + R)) : 1f;
                scaleL = scale;
                scaleR = scale;
                targetCenter = 0f;
            }

            if (v0 <= borderMin)
            {
                if (L <= 0.00001f) return targetMin;
                float t = (v0 - origMin) / L;
                return targetMin + t * (L * scaleL);
            }
            else if (v0 >= borderMax)
            {
                if (R <= 0.00001f) return targetMax;
                float t = (origMax - v0) / R;
                return targetMax - t * (R * scaleR);
            }
            else
            {
                float t = (v0 - borderMin) / origCenter;
                return targetMin + (L * scaleL) + t * targetCenter;
            }
        }

        /// <summary>
        /// Deforms a 3D vertex position according to slice settings and target bounds.
        /// </summary>
        public static Vector3 DeformVertex(
            Vector3 v0,
            Bounds origBounds,
            SliceBorder3D borders,
            Vector3 targetMin,
            Vector3 targetMax)
        {
            return new Vector3(
                DeformAxisCoordinate(v0.x, origBounds.min.x, origBounds.max.x, borders.x, targetMin.x, targetMax.x),
                DeformAxisCoordinate(v0.y, origBounds.min.y, origBounds.max.y, borders.y, targetMin.y, targetMax.y),
                DeformAxisCoordinate(v0.z, origBounds.min.z, origBounds.max.z, borders.z, targetMin.z, targetMax.z)
            );
        }

        /// <summary>
        /// Deforms a vertex array in-place or into an output array.
        /// </summary>
        public static void DeformVertices(
            Vector3[] origVertices,
            Vector3[] outVertices,
            Bounds origBounds,
            SliceBorder3D borders,
            Vector3 targetMin,
            Vector3 targetMax,
            Matrix4x4 localToRoot,
            Matrix4x4 rootToLocal)
        {
            bool isIdentity = localToRoot.isIdentity;

            for (int i = 0; i < origVertices.Length; i++)
            {
                Vector3 vRoot = isIdentity ? origVertices[i] : localToRoot.MultiplyPoint3x4(origVertices[i]);
                Vector3 deformedRoot = DeformVertex(vRoot, origBounds, borders, targetMin, targetMax);
                outVertices[i] = isIdentity ? deformedRoot : rootToLocal.MultiplyPoint3x4(deformedRoot);
            }
        }

        public static float ConvertUnitsToMeters(float value, MeasurementUnit unit)
        {
            switch (unit)
            {
                case MeasurementUnit.Centimeters: return value * 0.01f;
                case MeasurementUnit.Millimeters: return value * 0.001f;
                case MeasurementUnit.Inches: return value * 0.0254f;
                case MeasurementUnit.Meters:
                default: return value;
            }
        }

        public static float ConvertMetersToUnits(float meters, MeasurementUnit unit)
        {
            switch (unit)
            {
                case MeasurementUnit.Centimeters: return meters * 100f;
                case MeasurementUnit.Millimeters: return meters * 1000f;
                case MeasurementUnit.Inches: return meters / 0.0254f;
                case MeasurementUnit.Meters:
                default: return meters;
            }
        }

        public static Vector3 ConvertUnitsToMeters(Vector3 value, MeasurementUnit unit)
        {
            return new Vector3(
                ConvertUnitsToMeters(value.x, unit),
                ConvertUnitsToMeters(value.y, unit),
                ConvertUnitsToMeters(value.z, unit)
            );
        }

        public static Vector3 ConvertMetersToUnits(Vector3 meters, MeasurementUnit unit)
        {
            return new Vector3(
                ConvertMetersToUnits(meters.x, unit),
                ConvertMetersToUnits(meters.y, unit),
                ConvertMetersToUnits(meters.z, unit)
            );
        }
    }
}
