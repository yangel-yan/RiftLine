using UnityEngine;

namespace Riftline
{
    internal static class RiftlineAppearance
    {
        public static void Apply(GameObject root, int outfit, Color tint)
        {
            // Keep every generated base/normal texture and every material slot.
            Color multiplier = outfit == 0 ? Color.white : Color.Lerp(Color.white, tint, .55f);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is LineRenderer || renderer is ParticleSystemRenderer) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i] || !materials[i].HasProperty("_BaseColor")) continue;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, i);
                    block.SetColor("_BaseColor", materials[i].GetColor("_BaseColor") * multiplier);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
