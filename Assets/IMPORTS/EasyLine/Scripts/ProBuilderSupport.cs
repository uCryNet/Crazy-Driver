using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace EasyLine
{
    /// <summary>
    /// Optional bridge that lets the SplineMeshDeformer consume ProBuilder objects as a
    /// geometry source. ProBuilder support is an *add-on*: this class talks to ProBuilder
    /// purely through reflection, so EasyLine compiles and runs identically whether or not
    /// the ProBuilder package is installed. There is no hard assembly reference and no
    /// scripting-define / ProjectSettings manipulation.
    ///
    /// Why it is needed:
    /// A ProBuilder object stores its real geometry inside the <c>ProBuilderMesh</c> component
    /// (serialized positions/faces/uvs). The visible <c>MeshFilter.sharedMesh</c>
    /// (e.g. "pb_Mesh-25478") is a volatile mesh that ProBuilder regenerates at load time. On a
    /// *prefab asset* that runtime mesh is frequently <c>null</c> or not readable, so the deformer
    /// would extract nothing. Here we rebuild a clean, readable UnityEngine.Mesh straight from the
    /// ProBuilderMesh data via ProBuilder's public, non-destructive <c>MeshUtility.Compile</c>.
    ///
    /// All reflection lookups are cached once and only ever run during a static-mesh rebuild
    /// (i.e. when geometry/source changes) - never inside the per-frame deformation loop - so
    /// there is no runtime/FPS cost on the hot path.
    /// </summary>
    public static class ProBuilderSupport
    {
        // --- Cached reflection handles (resolved once) ---
        private static bool s_Resolved;
        private static Type s_ProBuilderMeshType;   // UnityEngine.ProBuilder.ProBuilderMesh
        private static MethodInfo s_CompileMethod;   // UnityEngine.ProBuilder.MeshUtility.Compile(pb, mesh, topology)

        // One owned target mesh per ProBuilderMesh instance, so we refresh in place instead of
        // leaking a new Mesh on every rebuild. Pruned of destroyed entries.
        private static readonly Dictionary<Component, Mesh> s_Compiled = new Dictionary<Component, Mesh>();

        private static void Resolve()
        {
            if (s_Resolved) return;
            s_Resolved = true;

            s_ProBuilderMeshType = FindType("UnityEngine.ProBuilder.ProBuilderMesh");

            // MeshUtility ships in the same assembly as ProBuilderMesh, so resolve it straight
            // from there instead of paying for a second assembly scan. If ProBuilderMesh was not
            // found the package is absent and there is nothing to look for anyway.
            Type meshUtility = null;
            if (s_ProBuilderMeshType != null)
            {
                try { meshUtility = s_ProBuilderMeshType.Assembly.GetType("UnityEngine.ProBuilder.MeshUtility"); }
                catch { meshUtility = null; }
            }

            if (s_ProBuilderMeshType != null && meshUtility != null)
            {
                // public static void Compile(ProBuilderMesh, Mesh, MeshTopology)
                // MeshTopology lives in UnityEngine, so we can reference it directly.
                s_CompileMethod = meshUtility.GetMethod(
                    "Compile",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { s_ProBuilderMeshType, typeof(Mesh), typeof(MeshTopology) },
                    null);
            }
        }

        private static Type FindType(string fullName)
        {
            // Fast path: assembly-qualified name.
            Type t = Type.GetType(fullName + ", Unity.ProBuilder");
            if (t != null) return t;

            // Fallback: scan loaded assemblies (covers renamed/repackaged assemblies).
            // Uses Unity's own registry instead of AppDomain.CurrentDomain.GetAssemblies(), which
            // may hand back already-unloaded assemblies and leak or throw (analyzer UAC0005).
            // TypeCache is not an option here - it is editor-only and this is runtime code.
            IReadOnlyList<Assembly> loaded = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
            if (loaded == null) return null;

            for (int i = 0; i < loaded.Count; i++)
            {
                try { t = loaded[i].GetType(fullName); }
                catch { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>True if the ProBuilder package is present and its compile API is reachable.</summary>
        public static bool IsAvailable
        {
            get { Resolve(); return s_ProBuilderMeshType != null && s_CompileMethod != null; }
        }

        /// <summary>True if the given MeshFilter's GameObject carries a ProBuilderMesh component.</summary>
        public static bool IsProBuilderBacked(MeshFilter mf)
        {
            Resolve();
            if (mf == null || s_ProBuilderMeshType == null) return false;
            return mf.GetComponent(s_ProBuilderMeshType) != null;
        }

        /// <summary>True if the GameObject (or any child) contains at least one ProBuilderMesh.</summary>
        public static bool ContainsProBuilder(GameObject go)
        {
            Resolve();
            if (go == null || s_ProBuilderMeshType == null) return false;
            return go.GetComponentInChildren(s_ProBuilderMeshType, true) != null;
        }

        /// <summary>
        /// Removes the ProBuilderMesh component from a MeshFilter's GameObject. Used after baking a
        /// custom (e.g. deformed) mesh onto a generated instance, so ProBuilder cannot rebuild and
        /// overwrite that mesh on the next refresh. No-op when ProBuilder is absent or not present
        /// on the object. The MeshFilter and MeshRenderer are left untouched.
        /// </summary>
        public static void StripProBuilder(MeshFilter mf)
        {
            Resolve();
            if (mf == null || s_ProBuilderMeshType == null) return;
            Component comp = mf.GetComponent(s_ProBuilderMeshType);
            if (comp == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(comp);
            else UnityEngine.Object.Destroy(comp);
#else
            UnityEngine.Object.Destroy(comp);
#endif
        }

        /// <summary>
        /// Returns the best renderable mesh for a MeshFilter.
        /// Normal objects -> their sharedMesh untouched.
        /// ProBuilder objects whose sharedMesh is null or non-readable (typical for prefab
        /// assets) -> a freshly compiled, readable mesh built from the ProBuilderMesh data.
        /// When ProBuilder is absent this is just <c>mf.sharedMesh</c> plus one cheap check.
        /// </summary>
        public static Mesh ResolveRenderMesh(MeshFilter mf)
        {
            if (mf == null) return null;
            Mesh sm = mf.sharedMesh;

            // A live, readable mesh (e.g. a ProBuilder object already in the scene, or any normal
            // imported mesh) is already correct and cheaper to use directly.
            if (sm != null && sm.isReadable) return sm;

            Resolve();
            if (s_CompileMethod == null) return sm; // ProBuilder not installed -> original behaviour

            Component pb = mf.GetComponent(s_ProBuilderMeshType);
            if (pb == null) return sm;

            Mesh compiled = Compile(pb);
            return compiled != null ? compiled : sm;
        }

        private static Mesh Compile(Component pb)
        {
            if (pb == null || s_CompileMethod == null) return null;

            // MeshUtility.Compile derives the submesh count from the Renderer's material count,
            // so a Renderer is required. Every ProBuilder object has one, but guard anyway.
            if (pb.GetComponent<Renderer>() == null) return null;

            if (!s_Compiled.TryGetValue(pb, out Mesh target) || target == null)
            {
                PruneDestroyed();
                target = new Mesh { name = "EasyLine_PBCompiled", hideFlags = HideFlags.HideAndDontSave };
                s_Compiled[pb] = target;
            }

            try
            {
                s_CompileMethod.Invoke(null, new object[] { pb, target, MeshTopology.Triangles });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EasyLine] Failed to compile ProBuilder mesh on '{pb.name}': {e.Message}");
                return null;
            }

            return target.vertexCount > 0 ? target : null;
        }

        private static void PruneDestroyed()
        {
            List<Component> dead = null;
            foreach (var kv in s_Compiled)
            {
                if (kv.Key == null) // Unity's overloaded == catches destroyed objects
                    (dead ??= new List<Component>()).Add(kv.Key);
            }
            if (dead == null) return;
            foreach (var k in dead)
            {
                if (s_Compiled.TryGetValue(k, out Mesh m) && m != null)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(m);
#else
                    UnityEngine.Object.Destroy(m);
#endif
                }
                s_Compiled.Remove(k);
            }
        }
    }
}
