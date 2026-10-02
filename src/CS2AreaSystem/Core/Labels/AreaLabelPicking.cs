using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Areas;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Core.Labels
{
    // Label clicks with the vanilla district label math (Game.Areas.RaycastJobs.RaycastLabelsJob): the label faces the
    // camera, scales with camera distance, and every text line is its own click box.
    public static class AreaLabelPicking
    {
        public static bool TryHit(Line3.Segment ray, float3 cameraRight, float3 labelPosition, IReadOnlyList<Bounds2> lines, out float distance)
        {
            distance = float.MaxValue;
            if (lines == null || lines.Count == 0) return false;
            var matrix = AreaUtils.CalculateLabelMatrix(ray.a, labelPosition, AreaUtils.CalculateLabelRotation(cameraRight));
            float scale = AreaUtils.CalculateLabelScale(ray.a, labelPosition);
            float length = math.max(1f, MathUtils.Length(ray));
            bool hit = false;
            foreach (var bounds in lines)
            {
                var quad = new Quad3(
                    math.transform(matrix, new float3(bounds.min.xy, 0f)),
                    math.transform(matrix, new float3(bounds.min.x, bounds.max.y, 0f)),
                    math.transform(matrix, new float3(bounds.max.xy, 0f)),
                    math.transform(matrix, new float3(bounds.max.x, bounds.min.y, 0f)));
                if (!MathUtils.Intersect(quad, ray, out float t)) continue;
                float candidate = t - MathUtils.Size(bounds.y) * scale / length;
                if (candidate < distance) distance = candidate;
                hit = true;
            }
            return hit;
        }
        // The nearest label on the ray; equal distances pick the lower entity index so the result never depends on order.
        public static bool TryPick(Line3.Segment ray, float3 cameraRight, IReadOnlyList<(Entity area, float3 position, IReadOnlyList<Bounds2> lines)> labels,
            out Entity area, out float distance)
        {
            area = Entity.Null;
            distance = float.MaxValue;
            if (labels == null) return false;
            foreach (var label in labels)
            {
                if (!TryHit(ray, cameraRight, label.position, label.lines, out float candidate)) continue;
                if (area == Entity.Null || candidate < distance || (candidate == distance && label.area.Index < area.Index))
                { area = label.area; distance = candidate; }
            }
            return area != Entity.Null;
        }
        // Runs after DefaultToolSystem handled the press. Returns the Area to select, or Entity.Null to keep vanilla's
        // selection: the label wins only when it is strictly nearer than whatever vanilla hit.
        public static Entity ResolveClick(bool pressed, bool overWorld, bool defaultToolActive,
            bool labelHit, Entity label, float labelDistance, bool vanillaHit, float vanillaDistance)
        {
            if (!pressed || !overWorld || !defaultToolActive || !labelHit || label == Entity.Null) return Entity.Null;
            return !vanillaHit || labelDistance < vanillaDistance ? label : Entity.Null;
        }
    }
}
