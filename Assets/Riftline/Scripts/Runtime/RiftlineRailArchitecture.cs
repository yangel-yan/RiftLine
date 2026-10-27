using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Riftline
{
    /// <summary>Shared, collider-free structure surrounding the navigable rail.</summary>
    public sealed class RiftlineRailArchitecture : MonoBehaviour
    {
        const int CellCount = 9;
        const float CellLength = 14f;
        readonly Transform[] cells = new Transform[CellCount];
        Mesh frameMesh, lightMesh;
        Material metal, light;
        RiftlineWorld world;
        int region = -1;
        bool dirty = true;

        public void Initialize(RiftlineGame owner, RiftlineWorld rail)
        {
            world = rail;
            var frame = new List<Bounds>();
            var strips = new List<Bounds>();
            for (int side = -1; side <= 1; side += 2)
            {
                frame.Add(new Bounds(new Vector3(side * 5.65f, 2.6f, 0), new Vector3(.65f, 6.8f, .7f)));
                frame.Add(new Bounds(new Vector3(side * 5.75f, -.45f, 0), new Vector3(2.1f, .65f, 13.9f)));
                frame.Add(new Bounds(new Vector3(side * 6.15f, .3f, 0), new Vector3(.22f, .22f, 13.9f)));
                frame.Add(new Bounds(new Vector3(side * 5.65f, .1f, 0), new Vector3(1.25f, .45f, 1.1f)));
                strips.Add(new Bounds(new Vector3(side * 5.3f, 3.25f, -.38f), new Vector3(.06f, 3.2f, .08f)));
                strips.Add(new Bounds(new Vector3(side * 4.75f, -.06f, 0), new Vector3(.08f, .08f, 13.9f)));
            }
            frame.Add(new Bounds(new Vector3(0, 5.85f, 0), new Vector3(12f, .45f, .7f)));
            frame.Add(new Bounds(new Vector3(0, 6.15f, 0), new Vector3(7.4f, .15f, 1.1f)));
            strips.Add(new Bounds(new Vector3(0, 5.6f, -.38f), new Vector3(9.9f, .07f, .08f)));
            frameMesh = BuildBoxes("Dock Shared Frames", frame);
            lightMesh = BuildBoxes("Dock Shared Light Strips", strips);
            metal = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Dock Structural Metal" };
            metal.SetFloat("_Metallic", .35f);
            metal.SetFloat("_Smoothness", .38f);
            metal.EnableKeyword("_EMISSION");
            metal.SetColor("_EmissionColor", new Color(.04f, .055f, .075f));
            light = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Dock Shared Emission" };
            light.EnableKeyword("_EMISSION");
            for (int i = 0; i < CellCount; i++)
            {
                var root = new GameObject("Dock Module " + i).transform;
                root.SetParent(transform, false);
                cells[i] = root;
                CreateSurface("Structure", root, frameMesh, metal);
                CreateSurface("Guide Lights", root, lightMesh, light);
            }
        }

        public void ResetRun() { region = -1; dirty = true; }

        public void Present(RunSimulation run)
        {
            if (run.RegionId != region)
            {
                region = run.RegionId;
                Color tint = region == 1 ? new Color(.6f, .16f, 1f) :
                    region == 2 ? new Color(.38f, 1f, .66f) : new Color(.09f, .75f, 1f);
                Color steel = region == 1 ? new Color(.44f, .4f, .52f) :
                    region == 2 ? new Color(.42f, .5f, .46f) : new Color(.44f, .5f, .58f);
                metal.SetColor("_BaseColor", steel);
                light.SetColor("_BaseColor", tint);
                light.SetColor("_EmissionColor", tint * 2f);
            }
            if (!dirty && run.Phase != RunPhase.Running) return;
            dirty = false;
            float start = Mathf.Floor((run.Distance - CellLength) / CellLength) * CellLength;
            for (int i = 0; i < CellCount; i++)
            {
                float distance = start + i * CellLength;
                cells[i].gameObject.SetActive(distance >= 0f);
                if (distance < 0f) continue;
                Pose pose = world.TrackPoseAt(distance);
                // A straight ledge must not span a right-angle junction and enter its exit lanes.
                Pose before = world.TrackPoseAt(Mathf.Max(0, distance - CellLength * .5f));
                Pose after = world.TrackPoseAt(distance + CellLength * .5f);
                if (Quaternion.Angle(before.rotation, after.rotation) > 1f)
                {
                    cells[i].gameObject.SetActive(false);
                    continue;
                }
                cells[i].SetPositionAndRotation(pose.position, pose.rotation);
            }
        }

        internal static void CreateSurface(string name, Transform parent, Mesh mesh, Material material)
        {
            var surface = new GameObject(name);
            surface.transform.SetParent(parent, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        internal static Mesh BuildBoxes(string name, List<Bounds> boxes)
        {
            var vertices = new List<Vector3>(boxes.Count * 24);
            var normals = new List<Vector3>(boxes.Count * 24);
            var indices = new List<int>(boxes.Count * 36);
            Vector3[] faceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Bounds box in boxes)
            {
                for (int face = 0; face < 6; face++)
                {
                    Vector3 normal = faceNormals[face];
                    Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                    Vector3 v = Vector3.Cross(normal, u);
                    int first = vertices.Count;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float a = corner == 0 || corner == 3 ? -1 : 1;
                        float b = corner < 2 ? -1 : 1;
                        vertices.Add(box.center + Vector3.Scale(normal + u * a + v * b, box.extents));
                        normals.Add(normal);
                    }
                    indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
                    indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        void OnDestroy()
        {
            if (frameMesh) Destroy(frameMesh);
            if (lightMesh) Destroy(lightMesh);
            if (metal) Destroy(metal);
            if (light) Destroy(light);
        }
    }
}
