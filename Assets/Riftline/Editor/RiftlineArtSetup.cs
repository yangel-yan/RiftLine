using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Riftline
{
    public static class RiftlineArtSetup
    {
        const string Art = "Assets/Riftline/Art/Generated/";

        [MenuItem("Riftline/Configure Generated Art")]
        public static void Configure()
        {
            ConfigureAvatar();
            ConfigureMeshLods();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) continue;
                importer.maxTextureSize = path.Contains("Skies/") ? 2048 : path.Contains("MenuArtwork") ? 2048 : 1024;
                importer.mipmapEnabled = !path.Contains("References") && !path.Contains("ActionIcons");
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Art }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material) continue;
                var path = AssetDatabase.GetAssetPath(material);
                if (path.Contains("_Assets/Materials/"))
                {
                    var textureFolder = Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace('\\', '/') + "/selected.fbm/";
                    var color = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + material.name + ".png");
                    var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + material.name.Replace("Color_", "NormalGL_") + ".png");
                    if (color) material.SetTexture("_BaseMap", color);
                    if (normal)
                    {
                        var normalImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                        if (normalImporter && normalImporter.textureType != TextureImporterType.NormalMap)
                        {
                            normalImporter.textureType = TextureImporterType.NormalMap;
                            normalImporter.SaveAndReimport();
                        }
                        material.SetTexture("_BumpMap", normal);
                        material.EnableKeyword("_NORMALMAP");
                    }
                }
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Art + "Animations" }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (!clip) continue;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = clip.name == "Idle" || clip.name == "Run" || clip.name == "RunStationary";
                settings.loopBlend = settings.loopTime;
                settings.keepOriginalPositionXZ = false;
                settings.keepOriginalPositionY = false;
                settings.keepOriginalOrientation = false;
                settings.loopBlendPositionXZ = true;
                settings.loopBlendPositionY = true;
                settings.loopBlendOrientation = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Riftline/Audio/Generated" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (!importer) continue;
                var settings = importer.defaultSampleSettings;
                settings.loadType = path.Contains("Music") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .65f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !path.Contains("Music");
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Riftline] Generated art configured.");
        }

        static void ConfigureMeshLods()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Art }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                if (!importer || importer.generateMeshLods) continue;
                importer.generateMeshLods = true;
                importer.maximumMeshLod = 4;
                importer.SaveAndReimport();
            }
            // The AI rig exporter saves its skinned geometry as native Mesh assets.
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { Art }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!mesh || mesh.lodCount > 1) continue;
                MeshLodUtility.GenerateMeshLods(mesh, 4);
                EditorUtility.SetDirty(mesh);
            }
        }

        static void ConfigureAvatar()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Eve.prefab");
            if (!source || source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                throw new InvalidOperationException("Unity AI Generator rigged Eve is required.");
            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = "EveRigged";
            try
            {
                var mapping = new Dictionary<string, string>
                {
                    { "Hips", "Hip" }, { "Spine", "Waist" }, { "Chest", "Spine01" }, { "UpperChest", "Spine02" },
                    { "Neck", "NeckTwist01" }, { "Head", "Head" },
                    { "LeftShoulder", "L_Clavicle" }, { "LeftUpperArm", "L_Upperarm" },
                    { "LeftLowerArm", "L_Forearm" }, { "LeftHand", "L_Hand" },
                    { "RightShoulder", "R_Clavicle" }, { "RightUpperArm", "R_Upperarm" },
                    { "RightLowerArm", "R_Forearm" }, { "RightHand", "R_Hand" },
                    { "LeftUpperLeg", "L_Thigh" }, { "LeftLowerLeg", "L_Calf" },
                    { "LeftFoot", "L_Foot" }, { "LeftToes", "L_ToeBase" },
                    { "RightUpperLeg", "R_Thigh" }, { "RightLowerLeg", "R_Calf" },
                    { "RightFoot", "R_Foot" }, { "RightToes", "R_ToeBase" }
                };
                var human = new List<HumanBone>();
                foreach (var pair in mapping)
                    human.Add(new HumanBone { humanName = pair.Key, boneName = pair.Value, limit = new HumanLimit { useDefaultValues = true } });
                var bindTransforms = clone.GetComponentsInChildren<Transform>(true);
                var bindPositions = bindTransforms.Select(bone => bone.localPosition).ToArray();
                var bindRotations = bindTransforms.Select(bone => bone.localRotation).ToArray();
                NormalizeReferencePose(clone);
                var skeleton = new List<SkeletonBone>();
                foreach (var bone in clone.GetComponentsInChildren<Transform>(true))
                    skeleton.Add(new SkeletonBone
                    {
                        name = bone.name, position = bone.localPosition,
                        rotation = bone.localRotation, scale = bone.localScale
                    });
                var avatar = AvatarBuilder.BuildHumanAvatar(clone, new HumanDescription
                {
                    human = human.ToArray(), skeleton = skeleton.ToArray(),
                    upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                    armStretch = .05f, legStretch = .05f, feetSpacing = 0, hasTranslationDoF = false
                });
                // Avatar reference axes are a T-pose; the skin keeps its generated bind matrices.
                for (int i = 0; i < bindTransforms.Length; i++)
                {
                    bindTransforms[i].localPosition = bindPositions[i];
                    bindTransforms[i].localRotation = bindRotations[i];
                }
                if (!avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Generated skeleton humanoid mapping failed.");
                avatar.name = "EveHumanoid";
                var existing = AssetDatabase.LoadAssetAtPath<Avatar>(Art + "EveHumanoid.asset");
                if (existing)
                {
                    EditorUtility.CopySerialized(avatar, existing);
                    UnityEngine.Object.DestroyImmediate(avatar);
                    avatar = existing;
                }
                else AssetDatabase.CreateAsset(avatar, Art + "EveHumanoid.asset");
                foreach (var childAnimator in clone.GetComponentsInChildren<Animator>(true))
                    UnityEngine.Object.DestroyImmediate(childAnimator);
                var animator = clone.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var skin in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
                PrefabUtility.SaveAsPrefabAsset(clone, Art + "EveRigged.prefab");
                Debug.Log("[Riftline] AI-generated skeleton mapped to a valid humanoid Avatar.");
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        static void NormalizeReferencePose(GameObject root)
        {
            var bones = root.GetComponentsInChildren<Transform>().ToDictionary(bone => bone.name);
            var hip = bones["Hip"];
            var children = hip.Cast<Transform>().ToArray();
            var positions = children.Select(bone => bone.position).ToArray();
            var rotations = children.Select(bone => bone.rotation).ToArray();
            // Exported Hip has arbitrary pre-rotation; Pelvis contains the anatomical frame.
            hip.rotation = bones["Pelvis"].rotation;
            for (int i = 0; i < children.Length; i++)
                children[i].SetPositionAndRotation(positions[i], rotations[i]);
            Aim(bones, "Waist", "Spine01", Vector3.up);
            Aim(bones, "Spine01", "Spine02", Vector3.up);
            Aim(bones, "Spine02", "NeckTwist01", Vector3.up);
            Aim(bones, "NeckTwist01", "Head", Vector3.up);
            foreach (string side in new[] { "L", "R" })
            {
                Vector3 direction = side == "L" ? Vector3.left : Vector3.right;
                Aim(bones, side + "_Thigh", side + "_Calf", Vector3.down);
                Aim(bones, side + "_Calf", side + "_Foot", Vector3.down);
                Aim(bones, side + "_Foot", side + "_ToeBase", Vector3.forward);
                Aim(bones, side + "_Upperarm", side + "_Forearm", direction);
                Aim(bones, side + "_Forearm", side + "_Hand", direction);
            }
        }

        static void Aim(Dictionary<string, Transform> bones, string name, string child, Vector3 direction)
        {
            Transform bone = bones[name];
            bone.rotation = Quaternion.FromToRotation(bones[child].position - bone.position, direction) * bone.rotation;
        }
    }
}
