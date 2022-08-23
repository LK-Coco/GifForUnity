using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class GifAssetImporter : AssetImporter
{
    public List<Texture2D> texs;

    public void SetData(string path)
    {
        var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        if (fs == null)
            Debug.LogError("Open file error!");

        int length = (int)fs.Length;
        byte[] gifData = new byte[length];
        fs.Read(gifData, 0, length);

        GifForUnity.GifDecoder decoder = new GifForUnity.GifDecoder(gifData);
        decoder.ProcessData();

        texs = decoder.resultTexs;
    }
}
