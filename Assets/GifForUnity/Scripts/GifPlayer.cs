using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace GifForUnity111
{
    /*
    public class GifTexture
    {
        public Texture2D texture;
        public float delaySec;
    }

    public class GifFormat
    {
        // 文件头
        public string Signature;
        public string Version;

        // 屏幕描述块
        public ushort LogicalScreenWidth;
        public ushort LogicalScreenHeight;
        public bool GlobalColorTableFlag;
        public int ColorResolution;
        public bool SortFlag;
        public int SizeOfGlobalColorTable;
        public byte BackgroundColorIndex;
        public byte PixelAspectRatio;

        // 全局调色盘数据
        public List<byte[]> GlobalColorTables; // rgb一组
        public List<GifImageBlock> GifImageBlocks;
        public List<GifGraphicControlExtension> GifGraphicControlExtensions;
        public List<GifCommentExtension> GifCommentExtensions;
        public List<GifPlainTextExtension> GifPlainTextExtensions;
        public GifApplicationExtension GifApplicationExtension;

        // 结束符
        public byte Trailer;
    }

    public class GifImageBlock
    {
        public byte ImageSeparator;
        public ushort LeftPosition;
        public ushort TopPosition;
        public ushort Width;
        public ushort Height;
        public bool LocalColorTableFlag;
        public bool InterlaceFlag;
        public bool SortFlag;
        public int SizeOfLocalColorTable;
        public List<byte[]> LocalColorTables;
        public byte LZWMinCodeLength;
        public struct SubImageBlock
        {
            public byte BlockSize;
            public byte[] BlockData;
        }
        public List<SubImageBlock> SubImageBlocks;
    }

    public class GifGraphicControlExtension
    {
        public byte ExtensionIntroducer;
        public byte GraphicControlLabel;
        public byte BlockSize;
        public ushort DiposalMethod;
        public bool UserInputFlag;
        public bool TransparentColorFlag;
        public ushort DelayTime;
        public byte TransparentColorIndex;
        public byte BlockTerminator;
    }

    public class GifCommentExtension
    {
        public byte ExtensionIntroducer;
        public byte CommentLabel;
        public List<CommentDataBlock> SubBlocks;

        public class CommentDataBlock
        {
            public byte BlockSize;
            public byte[] BlockDatas;
        }
    }

    public class GifPlainTextExtension
    {
        public byte ExtensionIntroducer;
        public byte PlainTextLabel;
        public byte BlockSize;
        public ushort TextGridLeftPosition;
        public ushort TextGridTopPosition;
        public ushort TextGridWidth;
        public ushort TextGridHeight;
        public byte CharacterCellWidth;
        public byte CharacterCellHeight;
        public byte TextForegroundColorIndex;
        public byte TextBackgroundColorIndex;
        public List<PlainTextData> PlainTextDatas;
        public class PlainTextData
        {
            public byte BlockSize;
            public byte[] BlockDatas;
        }
    }

    public class GifApplicationExtension
    {
        public byte ExtensionIntroducer;
        public byte ExtensionLabel;
        public byte BlockSize;

        public byte ApplicationID0;
        public byte ApplicationID1;
        public byte ApplicationID2;
        public byte ApplicationID3;
        public byte ApplicationID4;
        public byte ApplicationID5;
        public byte ApplicationID6;
        public byte ApplicationID7;

        public byte AppAutherCode0;
        public byte AppAutherCode1;
        public byte AppAutherCode2;

        public List<ApplicationData> ApplicationDatas;

        public class ApplicationData
        {
            public byte BlockSize;
            public byte[] BlockDatas;
        }
    }
    */

    public class GifPlayer : MonoBehaviour
    {
        public List<string> paths;
        public int select;
        public RawImage ui;

        public Texture2D Tex;


        private List<Texture2D> _texs = new List<Texture2D>();
        private int index = 0;
        // Start is called before the first frame update
        void Start()
        {
            //ebug.Log("uint:" + (uint)81);
            OpenGif(paths[select]);
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                if (index == _texs.Count) index = 0;
                Tex = _texs[index++];
            }
        }

        private void OpenGif(string filePath)
        {
            var fs = new FileStream(filePath,FileMode.Open,FileAccess.Read);
            if (fs == null)
                Debug.LogError("Open file error!");

            int length = (int)fs.Length;
            byte[] gifData = new byte[length];
            fs.Read(gifData, 0, length);

            GifForUnity.GifDecoder decoder = new GifForUnity.GifDecoder(gifData);
            decoder.ProcessData();

            _texs = decoder.resultTexs;

            var images = decoder.gifFormat.images;
            Debug.Log("图片长度:" + images.Count);
            GenerateBigTex1(images);
        }

        private void GenerateBigTex1(List<GifForUnity.GifImage> images)
        {
            var count = images.Count;
            long length = 0;
            for (int i = 0; i < count; i++)
            {
                length += images[i].RawImage.LongLength;
            }
            Color32[] colors = new Color32[length];
            long index = 0;
            for (int i = 0; i < count; i++)
            {
                var image = images[i];
                for (long j = 0; j < image.RawImage.LongLength; j++)
                {
                    colors[index] = image.RawImage[j];
                    index++;
                }
            }

            Tex = new Texture2D(images[0].Width, images[0].Height * count, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Tex.SetPixels32(colors);
            Tex.Apply();
        }


        /*
        private GifFormat SetGifData(byte[] gifData)
        {
            GifFormat gifFormat = new GifFormat();

            string tempSignature = new string(new char[] { (char)gifData[0], (char)gifData[1], (char)gifData[2] });
            if (tempSignature != "GIF")
            {
                Debug.LogError("Not a gif file!");
            }
            gifFormat.Signature = tempSignature;

            string tempVersion = new string(new char[] { (char)gifData[3], (char)gifData[4], (char)gifData[5] });
            if(tempVersion != "87a" && tempVersion != "89a")
            {
                Debug.LogError("Error! only supported 87a or 89a");
            }
            gifFormat.Version = tempVersion;

            gifFormat.LogicalScreenWidth = BitConverter.ToUInt16(gifData, 6);
            gifFormat.LogicalScreenHeight = BitConverter.ToUInt16(gifData, 8);

            // 1byte
            gifFormat.GlobalColorTableFlag = (gifData[10] & 128) == 128; // 1bit
            gifFormat.ColorResolution = gifData[10] & 112 switch
            {
                112 => 8,
                96 => 7,
                80 => 6,
                64 => 5,
                48 => 4,
                32 => 3,
                16 => 2,
                _ => 1,
            }; // 3bit
            gifFormat.SortFlag = (gifData[10] & 8) == 8; // 1bit
            int pow = (gifData[10] & 7) + 1;
            gifFormat.SizeOfGlobalColorTable = (int)Math.Pow(2, pow);

            gifFormat.BackgroundColorIndex = gifData[11];
            gifFormat.PixelAspectRatio = gifData[12];

            int byteIndex = 13;
            if (gifFormat.GlobalColorTableFlag)
            {
                gifFormat.GlobalColorTables = new List<byte[]>();
                for (int i = 0; i < gifFormat.SizeOfGlobalColorTable; i++)
                {
                    gifFormat.GlobalColorTables.Add(new byte[] { gifData[byteIndex], gifData[++byteIndex], gifData[++byteIndex] });
                }
            }


            while (true)
            {
                if (gifData[byteIndex] == 0x2c)
                {
                    SetGifImageData(gifData, ref byteIndex, ref gifFormat);
                } 
                else if(gifData[byteIndex] == 0x21)
                {
                    SetGifExData(gifData, ref byteIndex, ref gifFormat);
                }
                else
                {
                    gifFormat.Trailer = gifData[byteIndex];
                    byteIndex += 1;
                    break;
                }
            }

            return gifFormat;
        }

        private void DecodeDataToTexture(GifFormat gifFormat)
        {
            if(gifFormat.GifImageBlocks == null || gifFormat.GifImageBlocks.Count < 0)
            {
                Debug.LogError("No image in File");
                return;
            }

            var imageBlockCount = gifFormat.GifImageBlocks.Count;
            List<GifTexture> gifTextures = new List<GifTexture>(imageBlockCount);

            for (int i = 0; i < imageBlockCount; i++)
            {
                byte[] decodedData = GetDecodedData(gifFormat.GifImageBlocks[i]);
            }
        }

        private byte[] GetDecodedData(GifImageBlock imgBlock)
        {
            // Combine LZW compressed data
            List<byte> lzwData = new List<byte>();
            for (int i = 0; i < imgBlock.SubImageBlocks.Count; i++)
            {
                for (int k = 0; k < imgBlock.SubImageBlocks[i].BlockSize; k++)
                {
                    lzwData.Add(imgBlock.SubImageBlocks[i].BlockData[k]);
                }
            }

            // LZW decode
            int needDataSize = imgBlock.Height * imgBlock.Width;
            byte[] decodedData = DecodeGifLZW(lzwData, imgBlock.LZWMinCodeLength, needDataSize);

            // Sort interlace GIF
            if (imgBlock.InterlaceFlag)
            {
                //decodedData = SortInterlaceGifData(decodedData, imgBlock.Width);
            }
            return decodedData;
        }


        private void InitDic(
            Dictionary<int,string> dic,int lZWMinCodeLength,out int clearCode,out int finishCode,out int lzwCodeSize)
        {
            clearCode = (int)Math.Pow(2, lZWMinCodeLength);
            finishCode = clearCode + 1;

            for (int i = 0; i < clearCode + 2; i++)
            {
                dic.Add(i, ((char)i).ToString());
            }

            lzwCodeSize = lZWMinCodeLength + 1;
        }




        private byte[] DecodeGifLZW(List<byte> lzwData, int lZWMinCodeLength, int needDataSize)
        {
            int clearCode = 0;
            int finishCode = 0;

            int lzwCodeSize = 0;
            Dictionary<int, string> dic = new Dictionary<int, string>();

            InitDic(dic, lZWMinCodeLength, out clearCode, out finishCode, out lzwCodeSize);

            // Convert to bit array
            byte[] compDataArr = lzwData.ToArray();
            var bitData = new BitArray(compDataArr);

            byte[] output = new byte[needDataSize];
            int outputAddIndex = 0;

            string prevEntry = null;

            bool dicInitFlag = false;

            int bitDataIndex = 0;

            // LZW decode loop
            while (bitDataIndex < bitData.Length)
            {
                if (dicInitFlag)
                {
                    InitDic(dic, lZWMinCodeLength,  out clearCode, out finishCode, out lzwCodeSize);
                    dicInitFlag = false;
                }

                int key = bitData.GetNumeral(bitDataIndex, lzwCodeSize);

                string entry = null;

                if (key == clearCode)
                {
                    // Clear (Initialize dictionary)
                    dicInitFlag = true;
                    bitDataIndex += lzwCodeSize;
                    prevEntry = null;
                    continue;
                }
                else if (key == finishCode)
                {
                    // Exit
                    Debug.LogWarning("early stop code. bitDataIndex:" + bitDataIndex + " lzwCodeSize:" + lzwCodeSize + " key:" + key + " dic.Count:" + dic.Count);
                    break;
                }
                else if (dic.ContainsKey(key))
                {
                    // 存在于编译表中
                    entry = dic[key];
                    //output[outputAddIndex] = entry;
                }
                else if (key >= dic.Count)
                {
                    if (prevEntry != null)
                    {
                        // Output from estimation
                        entry = prevEntry + prevEntry[0];
                    }
                    else
                    {
                        Debug.LogWarning("It is strange that come here. bitDataIndex:" + bitDataIndex + " lzwCodeSize:" + lzwCodeSize + " key:" + key + " dic.Count:" + dic.Count);
                        bitDataIndex += lzwCodeSize;
                        continue;
                    }
                }
                else
                {
                    Debug.LogWarning("It is strange that come here. bitDataIndex:" + bitDataIndex + " lzwCodeSize:" + lzwCodeSize + " key:" + key + " dic.Count:" + dic.Count);
                    bitDataIndex += lzwCodeSize;
                    continue;
                }

                // Output
                // Take out 8 bits from the string.
                byte[] temp = Encoding.Unicode.GetBytes(entry);
                for (int i = 0; i < temp.Length; i++)
                {
                    if (i % 2 == 0)
                    {
                        output[outputAddIndex] = temp[i];
                        outputAddIndex++;
                    }
                }

                if (outputAddIndex >= needDataSize)
                {
                    // Exit
                    break;
                }

                if (prevEntry != null)
                {
                    // Add to dictionary
                    dic.Add(dic.Count, prevEntry + entry[0]);
                }

                prevEntry = entry;

                bitDataIndex += lzwCodeSize;

                if (lzwCodeSize == 3 && dic.Count >= 8)
                {
                    lzwCodeSize = 4;
                }
                else if (lzwCodeSize == 4 && dic.Count >= 16)
                {
                    lzwCodeSize = 5;
                }
                else if (lzwCodeSize == 5 && dic.Count >= 32)
                {
                    lzwCodeSize = 6;
                }
                else if (lzwCodeSize == 6 && dic.Count >= 64)
                {
                    lzwCodeSize = 7;
                }
                else if (lzwCodeSize == 7 && dic.Count >= 128)
                {
                    lzwCodeSize = 8;
                }
                else if (lzwCodeSize == 8 && dic.Count >= 256)
                {
                    lzwCodeSize = 9;
                }
                else if (lzwCodeSize == 9 && dic.Count >= 512)
                {
                    lzwCodeSize = 10;
                }
                else if (lzwCodeSize == 10 && dic.Count >= 1024)
                {
                    lzwCodeSize = 11;
                }
                else if (lzwCodeSize == 11 && dic.Count >= 2048)
                {
                    lzwCodeSize = 12;
                }
                else if (lzwCodeSize == 12 && dic.Count >= 4096)
                {
                    int nextKey = bitData.GetNumeral(bitDataIndex, lzwCodeSize);
                    if (nextKey != clearCode)
                    {
                        dicInitFlag = true;
                    }
                }
            }

            return output;
        }

        private byte[] SortInterlaceGifData(byte[] decodedData, ushort width)
        {
            return null;
        }

        private void SetGifImageData(byte[] gifData,ref int byteIndex,ref GifFormat gifFormat)
        {
            GifImageBlock imageBlock = new GifImageBlock();
            imageBlock.ImageSeparator = gifData[byteIndex];
            byteIndex += 1;
            imageBlock.LeftPosition = BitConverter.ToUInt16(gifData, byteIndex);
            byteIndex += 2;
            imageBlock.TopPosition = BitConverter.ToUInt16(gifData, byteIndex);
            byteIndex += 2;
            imageBlock.Width = BitConverter.ToUInt16(gifData, byteIndex);
            byteIndex += 2;
            imageBlock.Height = BitConverter.ToUInt16(gifData, byteIndex);
            byteIndex += 2;

            // 1byte
            imageBlock.LocalColorTableFlag = (gifData[byteIndex] & 128) == 128;
            imageBlock.InterlaceFlag = (gifData[byteIndex] & 64) == 64;
            imageBlock.SortFlag = (gifData[byteIndex] & 32) == 32;
            int val = (gifData[byteIndex] & 7) + 1;
            imageBlock.SizeOfLocalColorTable = (int)Math.Pow(2, val);
            byteIndex += 1;

            if (imageBlock.LocalColorTableFlag)
            {
                imageBlock.LocalColorTables = new List<byte[]>();
                for (int i = 0; i < imageBlock.SizeOfLocalColorTable; i++)
                {
                    imageBlock.LocalColorTables.Add(new byte[] { gifData[byteIndex], gifData[++byteIndex], gifData[++byteIndex] });
                }
            }

            imageBlock.LZWMinCodeLength = gifData[byteIndex];
            byteIndex += 1;

            while (true)
            {
                var subBlockSize = gifData[byteIndex];
                byteIndex += 1;
                if(subBlockSize == 0x00)
                {
                    break;
                }

                var subBlock = new GifImageBlock.SubImageBlock
                {
                    BlockSize = subBlockSize
                };
                subBlock.BlockData = new byte[subBlockSize];
                for (int i = 0; i < subBlockSize; i++, byteIndex++)
                {
                    subBlock.BlockData[i] = gifData[byteIndex];
                }
                if (imageBlock.SubImageBlocks == null)
                    imageBlock.SubImageBlocks = new List<GifImageBlock.SubImageBlock>();
                imageBlock.SubImageBlocks.Add(subBlock);
            }

            if (gifFormat.GifImageBlocks == null)
                gifFormat.GifImageBlocks = new List<GifImageBlock>();
            gifFormat.GifImageBlocks.Add(imageBlock);
        }

        private void SetGifExData(byte[] gifData,ref int byteIndex,ref GifFormat gifFormat)
        {
            switch (gifData[byteIndex+1])
            {
                case 0xf9:
                    // Graphic Control Extension(0x21 0xf9)
                    SetGraphicControlExtension(gifData, ref byteIndex, ref gifFormat);
                    break;
                case 0xfe:
                    // Comment Extension(0x21 0xfe)
                    SetCommentExtension(gifData, ref byteIndex, ref gifFormat);
                    break;
                case 0x01:
                    // Plain Text Extension(0x21 0x01)
                    SetPlainTextExtension(gifData, ref byteIndex, ref gifFormat);
                    break;
                case 0xff:
                    // Application Extension(0x21 0xff)
                    SetApplicationExtension(gifData, ref byteIndex, ref gifFormat);
                    break;
                default:
                    break;
            }
        }

        private void SetApplicationExtension(byte[] gifData, ref int byteIndex, ref GifFormat gifFormat)
        {
            var tempAppEx = new GifApplicationExtension();
            tempAppEx.ExtensionIntroducer = gifData[byteIndex];
            byteIndex += 1;

            tempAppEx.ExtensionLabel = gifData[byteIndex];
            byteIndex += 1;

            tempAppEx.BlockSize = gifData[byteIndex];
            byteIndex += 1;

            tempAppEx.ApplicationID0 = gifData[byteIndex];
            byteIndex += 1;
            tempAppEx.ApplicationID1 = gifData[byteIndex];
            byteIndex += 1;
            tempAppEx.ApplicationID2 = gifData[byteIndex];
            byteIndex += 1;
            tempAppEx.ApplicationID3 = gifData[byteIndex];
            byteIndex += 1;
            tempAppEx.ApplicationID4 = gifData[byteIndex];
            byteIndex += 1;
            tempAppEx.ApplicationID5 = gifData[byteIndex];
            byteIndex += 1;
            tempAppEx.ApplicationID6 = gifData[byteIndex];
            byteIndex += 1;
            tempAppEx.ApplicationID7 = gifData[byteIndex];
            byteIndex += 1;

            tempAppEx.AppAutherCode0 = gifData[byteIndex];
            byteIndex += 1; 
            tempAppEx.AppAutherCode1 = gifData[byteIndex];
            byteIndex += 1;
            tempAppEx.AppAutherCode2 = gifData[byteIndex];
            byteIndex += 1;

            while (true)
            {
                var blockSize = gifData[byteIndex];
                byteIndex += 1;
                if (blockSize == 0x00)
                {
                    break;
                }

                var blockData = new byte[blockSize];
                var plainData = new GifApplicationExtension.ApplicationData
                {
                    BlockSize = blockSize,
                    BlockDatas = blockData,
                };

                for (int i = 0; i < blockSize; i++, byteIndex++)
                {
                    blockData[i] = gifData[byteIndex];
                }

                if (tempAppEx.ApplicationDatas == null)
                    tempAppEx.ApplicationDatas = new List<GifApplicationExtension.ApplicationData>();
                tempAppEx.ApplicationDatas.Add(plainData);
            }

            gifFormat.GifApplicationExtension = tempAppEx;
        }

        private void SetPlainTextExtension(byte[] gifData, ref int byteIndex, ref GifFormat gifFormat)
        {
            var tempPlainEx = new GifPlainTextExtension();
            tempPlainEx.ExtensionIntroducer = gifData[byteIndex];
            byteIndex += 1;

            tempPlainEx.PlainTextLabel = gifData[byteIndex];
            byteIndex += 1;

            tempPlainEx.BlockSize = gifData[byteIndex];
            byteIndex += 1;

            tempPlainEx.TextGridLeftPosition = BitConverter.ToUInt16(gifData, byteIndex);
            byteIndex += 2;

            tempPlainEx.TextGridTopPosition = BitConverter.ToUInt16(gifData, byteIndex);
            byteIndex += 2;

            tempPlainEx.TextGridWidth = BitConverter.ToUInt16(gifData, byteIndex);
            byteIndex += 2;

            tempPlainEx.TextGridHeight = BitConverter.ToUInt16(gifData, byteIndex);
            byteIndex += 2;

            tempPlainEx.TextGridWidth = gifData[byteIndex];
            byteIndex += 1;

            tempPlainEx.TextGridHeight = gifData[byteIndex];
            byteIndex += 1;

            tempPlainEx.TextForegroundColorIndex = gifData[byteIndex];
            byteIndex += 1;

            tempPlainEx.TextBackgroundColorIndex = gifData[byteIndex];
            byteIndex += 1;

            while (true)
            {
                var blockSize = gifData[byteIndex];
                byteIndex += 1;
                if(blockSize == 0x00)
                {
                    break;
                }

                var blockData = new byte[blockSize];
                var plainData = new GifPlainTextExtension.PlainTextData
                {
                    BlockSize = blockSize,
                    BlockDatas = blockData,
                };

                for (int i = 0; i < blockSize; i++, byteIndex++)
                {
                    blockData[i] = gifData[byteIndex];
                }

                if (tempPlainEx.PlainTextDatas == null)
                    tempPlainEx.PlainTextDatas = new List<GifPlainTextExtension.PlainTextData>();
                tempPlainEx.PlainTextDatas.Add(plainData);
            }

            if (gifFormat.GifPlainTextExtensions == null)
                gifFormat.GifPlainTextExtensions = new List<GifPlainTextExtension>();
            gifFormat.GifPlainTextExtensions.Add(tempPlainEx);
        }

        private void SetCommentExtension(byte[] gifData, ref int byteIndex, ref GifFormat gifFormat)
        {
            var tempCommentEx = new GifCommentExtension();

            tempCommentEx.ExtensionIntroducer = gifData[byteIndex];
            byteIndex += 1;

            tempCommentEx.CommentLabel = gifData[byteIndex];
            byteIndex += 1;

            while (true)
            {
                var blockSize = gifData[byteIndex];
                byteIndex += 1;
                if(blockSize == 0x00)
                {
                    break;
                }

                var subBlock = new GifCommentExtension.CommentDataBlock
                {
                    BlockSize = blockSize,
                    BlockDatas = new byte[blockSize],
                };
                for (int i = 0; i < blockSize; i++, byteIndex++)
                {
                    subBlock.BlockDatas[i] = gifData[byteIndex]; 
                }
                if (tempCommentEx.SubBlocks == null)
                    tempCommentEx.SubBlocks = new List<GifCommentExtension.CommentDataBlock>();
                tempCommentEx.SubBlocks.Add(subBlock);
            }

            if (gifFormat.GifCommentExtensions == null)
                gifFormat.GifCommentExtensions = new List<GifCommentExtension>();
            gifFormat.GifCommentExtensions.Add(tempCommentEx);
        }

        private void SetGraphicControlExtension(byte[] gifData, ref int byteIndex, ref GifFormat gifFormat)
        {
            var tempGraphEx = new GifGraphicControlExtension();
            tempGraphEx.ExtensionIntroducer = gifData[byteIndex];
            byteIndex += 1;

            tempGraphEx.GraphicControlLabel = gifData[byteIndex];
            byteIndex += 1;

            tempGraphEx.BlockSize = gifData[byteIndex];
            byteIndex += 1;

            tempGraphEx.DiposalMethod = (ushort)(gifData[byteIndex] & 28 switch
            {
                4 => 1,
                8 => 2,
                12 => 3,
                _ => 0,
            });
            byteIndex += 1;

            tempGraphEx.DelayTime = BitConverter.ToUInt16(gifData, byteIndex);
            byteIndex += 2;

            tempGraphEx.TransparentColorIndex = gifData[byteIndex];
            byteIndex += 1;

            tempGraphEx.BlockTerminator = gifData[byteIndex];
            byteIndex += 1;

            if (gifFormat.GifGraphicControlExtensions == null)
                gifFormat.GifGraphicControlExtensions = new List<GifGraphicControlExtension>();
            gifFormat.GifGraphicControlExtensions.Add(tempGraphEx);
        }

        private void SetGifEndData()
        {

        }
*/
    }
}