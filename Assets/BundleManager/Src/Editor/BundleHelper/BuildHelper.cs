using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BundleManager
{
    public delegate List<UnityEngine.Object> FilterDelegate(UnityEngine.Object[] assets, BundleType bundleType, string assetPath);

    public static class BuildHelper
    {
        public static void RegisterFilter(FilterDelegate filterDelegate)
        {
            m_filterList.Add(filterDelegate);
        }
        public static void PushAssetDependencies()
        {
#pragma warning disable 0618
            BuildPipeline.PushAssetDependencies();
#pragma warning restore 0618
        }
        public static void PopAssetDependencies()
        {
#pragma warning disable 0618
            BuildPipeline.PopAssetDependencies();
#pragma warning restore 0618
        }
        public static bool BuildSingleBundle(BundleData bundle, BundleState state, bool pushDepend)
        {
            if (pushDepend)
                PushAssetDependencies();
            bool ret = BuildSingleBundle(bundle, state);
            if (pushDepend)
                PopAssetDependencies();
            return ret;
        }
        public static bool BuildSingleBundle(BundleData bundle, BundleState state)
        {
            if (bundle == null)
                return false;

            string outputPath = "";
            EditorCommon.CreateDirectory(outputPath);
            uint crc = 0;

            string[] assetPaths = bundle.includs.ToArray();

            bool succeed = false;
            if (bundle.type == BundleType.UnityMap)
            {
                succeed = BuildSceneBundle(assetPaths, outputPath, out crc);
            }
            else
            {
                succeed = BuildAssetBundle(assetPaths, outputPath, out crc, bundle.type);
            }

            succeed = UpdateBundleState(bundle, state, outputPath, succeed, crc);

            return succeed;
        }
        public static void FilterObjectByType(UnityEngine.Object[] assetsAtPath, List<UnityEngine.Object> ret, BundleType bundleType, string assetPath)
        {
            switch (bundleType)
            {
            case BundleType.FBX:
                foreach (UnityEngine.Object obj in assetsAtPath)
                {
                    if (obj == null)
                        continue;
                    Type type = obj.GetType();
                    if (type == typeof(AnimationClip) && obj.name != EditorConst.EDITOR_ANICLIP_NAME)
                    {
                        ret.Add(obj);
                    }
                    else
                    {
                        ret.Add(obj);
                    }
                }
                break;
            case BundleType.Controller:
                foreach (UnityEngine.Object obj in assetsAtPath)
                {
                    if (obj == null)
                        continue;
                    string typeName = obj.GetType().ToString();
                    if (typeName.Contains("AnimatorStateMachine") || typeName.Contains("AnimatorStateTransition") ||
                        typeName.Contains("AnimatorState") || typeName.Contains("AnimatorTransition") ||
                        typeName.Contains("BlendTree"))
                        continue;
                    ret.Add(obj);
                }

                break;
            default:
                ret.AddRange(assetsAtPath);
                break;
            }
            if (ret.Count == 0)
            {
                ret.AddRange(assetsAtPath);
            }

            for (int i = 0; i < m_filterList.Count; ++i)
            {
                ret = m_filterList[i](ret.ToArray(), bundleType, assetPath);
            }
        }
        private static bool UpdateBundleState(BundleData bundle, BundleState state, string outputPath, bool succeed, uint crc)
        {
            if (!succeed || (crc == state.crc && state.size > 0))
            {
                return succeed;
            }

            state.version++;
            if (state.version == int.MaxValue)
                state.version = 0;
            state.crc = crc;
            FileInfo bundleFileInfo = new FileInfo(outputPath);
            state.size = bundleFileInfo.Length;
            state.loadState = bundle.loadState;
            state.storePos = BundleStorePos.Building;

            return succeed;
        }
        private static bool BuildAssetBundle(string[] assetsList, string outputPath, out uint crc, BundleType bundleType)
        {
            crc = 0;
            // Load all of assets in this bundle
            List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            foreach (string assetPath in assetsList)
            {
                UnityEngine.Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                if (assetsAtPath != null || assetsAtPath.Length != 0)
                {
                    assets.AddRange(assetsAtPath);
                }
                else
                {
                    Debug.LogErrorFormat("Cannnot load [{0}] as asset object", assetPath);
                }
            }
            if (assets.Count == 0)
            {
                Debug.LogFormat("bundle name {0} empty", outputPath);
                return false;
            }

            // Build bundle
            return BuildAssetBundle(assets.ToArray(), outputPath, out crc);
        }
        private static bool BuildAssetBundle(UnityEngine.Object[] objs, string outputPath, out uint crc)
        {
            crc = 0;
#pragma warning disable 0618
            bool succeed = BuildPipeline.BuildAssetBundle(null, objs, outputPath, out crc,
                BuildConfig.CurrentBuildAssetOpts, BuildConfig.UnityBuildTarget);
#pragma warning restore 0618
            if (succeed)
            {
                crc = LZF.NET.CRC.CalculateDigest(outputPath);
            }
            else
            {
                Debug.LogErrorFormat("[BuildHelper] BuildAssetBundle Failed OutputPath = {0}.", outputPath);
            }
            return succeed;
        }
        private static bool BuildSceneBundle(string[] sceneList, string outputPath, out uint crc)
        {
            crc = 0;

            if (sceneList.Length == 0)
            {
                Debug.LogError("No scenes were provided for the scene bundle");
                return false;
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

#pragma warning disable 0618
            string error = BuildPipeline.BuildStreamedSceneAssetBundle(sceneList, outputPath,
                BuildConfig.UnityBuildTarget, out crc, BuildConfig.CurrentBuildSceneOpts);
#pragma warning restore 0618

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogErrorFormat("[BuildHelper] BuildSceneBundle error {0}.", error);
            }

            if (File.Exists(outputPath))
            {
                crc = LZF.NET.CRC.CalculateDigest(outputPath);
                return true;
            }

            return false;
        }
        private static List<FilterDelegate> m_filterList = new List<FilterDelegate>();
    }
}