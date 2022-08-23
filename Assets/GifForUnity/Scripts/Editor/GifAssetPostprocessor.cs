using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GifAssetPostprocessor : AssetPostprocessor
{
    //void OnPreprocessAsset()
    //{
    //    Debug.Log("pre load");
    //}


    void OnPreprocessTexture()
    {
        if (assetPath.EndsWith(".gif"))
        {
            var gifImporter = (GifAssetImporter)assetImporter;//AssetImporter.GetAtPath(assetPath) as GifAssetImporter;
            gifImporter.SetData(assetPath);
        }
    }
}
