using UnityEngine;
using UnityEngine.Rendering;

namespace Riftline
{
    public sealed class RiftlineEnvironment : MonoBehaviour
    {
        Material sky;
        RiftlineAssets assets;
        int reflectionRegion = -1;

        public void Initialize(RiftlineAssets library)
        {
            assets = library;
            if (!assets || !assets.SkyMaterial) return;
            sky = new Material(assets.SkyMaterial);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            Present(0, 0, 0);
        }

        public void Present(int from, int to, float blend)
        {
            if (!sky || assets.RegionSkies == null || assets.RegionSkies.Length < 3) return;
            sky.SetTexture("_TexA", assets.RegionSkies[from]);
            sky.SetTexture("_TexB", assets.RegionSkies[to]);
            sky.SetFloat("_Blend", blend);
            RenderSettings.ambientSkyColor = Color.Lerp(SkyLight(from), SkyLight(to), blend);
            RenderSettings.ambientEquatorColor = Color.Lerp(EquatorLight(from), EquatorLight(to), blend);
            RenderSettings.ambientGroundColor = new Color(.24f, .29f, .36f);
            int selected = blend < .5f ? from : to;
            if (reflectionRegion != selected)
            {
                reflectionRegion = selected;
                RenderSettings.customReflectionTexture = assets.RegionSkies[selected];
                RenderSettings.reflectionIntensity = .65f;
            }
        }

        static Color SkyLight(int id) => id == 0 ? new Color(.58f, .68f, .82f) :
            id == 1 ? new Color(.5f, .47f, .72f) : new Color(.52f, .72f, .64f);
        static Color EquatorLight(int id) => id == 0 ? new Color(.48f, .57f, .7f) :
            id == 1 ? new Color(.44f, .46f, .62f) : new Color(.44f, .62f, .56f);

        void OnDestroy()
        {
            if (sky)
            {
                if (RenderSettings.skybox == sky) RenderSettings.skybox = assets.SkyMaterial;
                Destroy(sky);
            }
        }
    }
}
