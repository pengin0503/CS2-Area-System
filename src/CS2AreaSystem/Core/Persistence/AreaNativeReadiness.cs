using System;
using System.Collections.Generic;
using Game.Areas;
using Unity.Entities;
using Unity.Mathematics;
namespace CS2AreaSystem.Core.Persistence
{
    public enum AreaNativeReadinessResult { Waiting, Ready, Quarantine }
    public enum AreaNativeGeometryProblem { None, MissingNativeArea, MissingBuffers, EmptyGeometry, InvalidGeometry }
    // Actual per-entity geometry, rather than only chunk change versions: another Area in the same chunk can change
    // those versions without changing this polygon. Versions still prove that search has processed the latest writes.
    public readonly struct AreaNativeGeometryEvidence
    {
        public ulong Fingerprint { get; }
        public uint NodeVersion { get; }
        public uint TriangleVersion { get; }
        public int NodeCount { get; }
        public int TriangleCount { get; }
        public AreaNativeGeometryProblem Problem { get; }
        private AreaNativeGeometryEvidence(ulong fingerprint, uint nodeVersion, uint triangleVersion, int nodes, int triangles, AreaNativeGeometryProblem problem)
        { Fingerprint = fingerprint; NodeVersion = nodeVersion; TriangleVersion = triangleVersion; NodeCount = nodes; TriangleCount = triangles; Problem = problem; }
        public static AreaNativeGeometryEvidence Read(bool nativeArea, int nodeCount, int triangleCount,
            Func<int, Node> nodeAt, Func<int, Triangle> triangleAt, uint nodeVersion, uint triangleVersion)
        {
            var problem = !nativeArea ? AreaNativeGeometryProblem.MissingNativeArea :
                nodeCount < 0 || triangleCount < 0 ? AreaNativeGeometryProblem.MissingBuffers :
                nodeCount < 3 || triangleCount == 0 ? AreaNativeGeometryProblem.EmptyGeometry : AreaNativeGeometryProblem.None;
            ulong hash = 14695981039346656037UL;
            Mix(ref hash, nativeArea ? 1u : 0u); Mix(ref hash, unchecked((uint)nodeCount)); Mix(ref hash, unchecked((uint)triangleCount));
            for (int i = 0; i < nodeCount; i++)
            {
                var node = nodeAt(i);
                Mix(ref hash, math.asuint(node.m_Position.x)); Mix(ref hash, math.asuint(node.m_Position.y));
                Mix(ref hash, math.asuint(node.m_Position.z)); Mix(ref hash, math.asuint(node.m_Elevation));
                if (!math.all(math.isfinite(node.m_Position)) || !math.isfinite(node.m_Elevation)) problem = AreaNativeGeometryProblem.InvalidGeometry;
            }
            for (int i = 0; i < triangleCount; i++)
            {
                var triangle = triangleAt(i);
                Mix(ref hash, unchecked((uint)triangle.m_Indices.x)); Mix(ref hash, unchecked((uint)triangle.m_Indices.y)); Mix(ref hash, unchecked((uint)triangle.m_Indices.z));
                Mix(ref hash, math.asuint(triangle.m_HeightRange.min)); Mix(ref hash, math.asuint(triangle.m_HeightRange.max)); Mix(ref hash, unchecked((uint)triangle.m_MinLod));
                if (math.any(triangle.m_Indices < 0) || math.any(triangle.m_Indices >= nodeCount) ||
                    !math.isfinite(triangle.m_HeightRange.min) || !math.isfinite(triangle.m_HeightRange.max)) problem = AreaNativeGeometryProblem.InvalidGeometry;
            }
            return new AreaNativeGeometryEvidence(hash, nodeVersion, triangleVersion, nodeCount, triangleCount, problem);
        }
        private static void Mix(ref ulong hash, uint value) => hash = unchecked((hash ^ value) * 1099511628211UL);
        public bool SearchPredatesGeometry(uint searchVersion) =>
            (NodeCount >= 0 && ChangeVersionUtility.DidChange(NodeVersion, searchVersion)) ||
            (TriangleCount >= 0 && ChangeVersionUtility.DidChange(TriangleVersion, searchVersion));
    }
    // This is a native-work evidence gate, not a frame timeout. One missing observation waits for initialization.
    // Another distinct completed search update with unchanged per-Area geometry proves that merely waiting for that
    // search pass cannot fix this Area. Session quarantine lets healthy Areas recover without editing saved geometry.
    public sealed class AreaNativeReadiness
    {
        private readonly Dictionary<Entity, Missing> missing = new Dictionary<Entity, Missing>();
        private uint loadVersion;
        private readonly struct Missing
        {
            public readonly uint SearchVersion;
            public readonly ulong Fingerprint;
            public readonly AreaNativeGeometryProblem Problem;
            public Missing(uint version, AreaNativeGeometryEvidence geometry) { SearchVersion = version; Fingerprint = geometry.Fingerprint; Problem = geometry.Problem; }
        }
        public void BeginLoad(uint searchVersion) { loadVersion = searchVersion; missing.Clear(); }
        public AreaNativeReadinessResult Observe(Entity area, bool nativeReady, uint searchVersion, AreaNativeGeometryEvidence geometry)
        {
            if (searchVersion == loadVersion || geometry.SearchPredatesGeometry(searchVersion))
            { missing.Remove(area); return AreaNativeReadinessResult.Waiting; }
            if (nativeReady && geometry.Problem == AreaNativeGeometryProblem.None)
            { missing.Remove(area); return AreaNativeReadinessResult.Ready; }
            if (missing.TryGetValue(area, out var previous) && previous.Fingerprint == geometry.Fingerprint && previous.Problem == geometry.Problem)
            {
                if (previous.SearchVersion != searchVersion) return AreaNativeReadinessResult.Quarantine;
                return AreaNativeReadinessResult.Waiting;
            }
            missing[area] = new Missing(searchVersion, geometry);
            return AreaNativeReadinessResult.Waiting;
        }
    }
}
