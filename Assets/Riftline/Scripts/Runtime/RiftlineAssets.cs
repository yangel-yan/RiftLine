using UnityEngine;

namespace Riftline
{
    [CreateAssetMenu(menuName = "Riftline/Asset Library")]
    public sealed class RiftlineAssets : ScriptableObject
    {
        public Texture2D MenuArtwork;
        public Texture2D ActionIcons;
        public GameObject EvePrefab;
        public GameObject CockpitPrefab;
        public GameObject PlatformPrefab;
        public GameObject CollectiblePrefab;
        public GameObject ControllerPrefab;
        public GameObject[] ObstaclePrefabs = new GameObject[8];
        public GameObject[] LandmarkPrefabs = new GameObject[3];
        public Material[] RegionMaterials = new Material[3];
        public Cubemap[] RegionSkies = new Cubemap[3];
        public Material SkyMaterial;
        public Material[] OutfitMaterials = new Material[8];
        public AnimationClip[] Animations;
        public AudioClip Music;
        public AudioClip[] Sounds;
    }
}
