using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace GifForUnity
{

    public class GifFormat
    {
        public string Version;
        public ushort CanvasWidth;
        public ushort CanvasHeight;
        public Color32[] GlobalColorTable;
        public Color32 BackgroundColor;

        public List<GifImage> images = new List<GifImage>();
    }

    public class GifImage
    {
        public int Width;
        public int Height;
        public int Delay;
        public Color32[] RawImage;

        public Texture2D CreateTexture()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            tex.SetPixels32(RawImage);
            tex.Apply();

            return tex;
        }
    }

    public class GifDecoder
    {
        #region Field

        public List<Texture2D> resultTexs = new List<Texture2D>();

        public byte[] gifData;
        public GifFormat gifFormat;
        private int _byteIndex;

        #endregion

        #region Constructors

        public GifDecoder(byte[] gifData)
        {
            this.gifData = gifData;
        }

        #endregion

        #region CurrentGraphicControlData

        private ushort _transparentColorIndex;
        private int _delay;
        private Color32[] _output;
        private Color32[] _previousImage;

        #endregion

        #region CurrentImageData

        private int _imageLeft; // 该帧图像绘制起点的左值（x）
        private int _imageTop; // 该帧图像绘制起点的顶值（y）
        private int _imageWidth; // 该帧图像绘制的宽度
        private int _imageHeight; // 该帧图像绘制的高度

        #endregion

        #region Data for LZWDecompress

        private int[] _indices = new int[4096]; // 编译表 ，存储了实际code在codes中的下标
        private ushort[] _codes = new ushort[128 * 1024]; // 颜色表，存储实际的code
        private uint[] _curBlock = new uint[64]; // 当前Image sub block 的数据 最多 256/4

        #endregion

        #region Const Value

        private const uint NoCode = 0xFFFF;
        private const ushort NoTransparency = 0xFFFF;
        private readonly int[] Pow2 = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 };

        #endregion

        #region Enums

        [Flags]
        enum LogicalScreenFlags
        {
            GlobalColorTableFlag = 0x80,
            ColorResolution = 0x70,
            SortFlag = 0x08,
            SizeOfGlobalColorTable = 0x07,
        }

        enum Block
        {
            Image = 0x2C,
            Extension = 0x21,
            End = 0x3B
        }

        [Flags]
        enum ImageFlags
        {
            LocalColorTableFlag = 0x80,
            InterlaceFlag = 0x40,
            SortFlag = 0x20,
            ReservedForFutureUse = 0x18,
            SizeOfLocalColorTable = 0x07,
        }

        enum Extension
        {
            GraphicControl = 0xF9,
            Comments = 0xFE,
            PlainText = 0x01,
            ApplicationData = 0xFF
        }

        enum GraphicControlFlags
        {
            TransparentColorFlag = 0x01,
            UserInputFlag = 0x02,
            DisposalMethod = 0x1C,
            ReservedForFutureUse = 0xE0,
        }

        enum DisposalMethod
        {
            None = 0x00,
            DoNotDispose = 0x04,
            RestoreBackground = 0x08,
            ReturnToPrevious = 0x0C
        }

        #endregion

        #region Public Method

        public void ProcessData()
        {
            gifFormat = new GifFormat();

            ReadHeader();

            while (true)
            {
                if (_byteIndex >= gifData.Length) return;
                var block = (Block)ReadByte();
                switch (block)
                {
                    case Block.Image:
                        var temp = ReadImageBlock();
                        if(temp != null)
                        {
                            gifFormat.images.Add(temp);
                            resultTexs.Add(temp.CreateTexture());
                        }
                        break;
                    case Block.Extension:
                        var subBlock = (Extension)ReadByte();
                        switch (subBlock)
                        {
                            case Extension.GraphicControl:
                                ReadGraphicControlBlock();
                                break;
                            default:
                                SkipBlock();
                                break;
                        }
                        break;
                    case Block.End:
                        break;
                    default:
                        throw new Exception("Unexpected block type");
                }
            }
        }

        #endregion

        #region Private Method

        private GifImage ReadImageBlock()
        {
            _imageLeft = ReadUInt16();
            _imageTop = ReadUInt16();
            _imageWidth = ReadUInt16();
            _imageHeight = ReadUInt16();

            var flags = (ImageFlags)ReadByte();

            if(gifFormat.CanvasWidth == 0 || gifFormat.CanvasHeight == 0)
            {
                return null;
            }

            var image = new GifImage
            {
                Width = gifFormat.CanvasWidth,
                Height = gifFormat.CanvasHeight,
            };
            Color32[] activeColorTable;
            if (flags.HasFlag(ImageFlags.LocalColorTableFlag))
            {
                var tableSize = Pow2[(int)(flags & ImageFlags.SizeOfLocalColorTable) + 1];
                activeColorTable = ReadColorTable(tableSize);
            }
            else
            {
                activeColorTable = gifFormat.GlobalColorTable;
            }

            if (_output == null)
            {
                _output = new Color32[image.Width * image.Height];
                _previousImage = _output;
            }

            LZWDecompress(activeColorTable, image.Width, image.Height);

            if(flags.HasFlag(ImageFlags.InterlaceFlag))
            {
                Interlace();
            }

            image.RawImage = _output;
            image.Delay = _delay;
            return image;
        }
     
        private void Interlace()
        {
            var numRows = _output.Length / gifFormat.CanvasWidth;
            var writePos = _output.Length - gifFormat.CanvasWidth; // NB: work backwards due to Y-coord flip
            var input = _output;

            _output = new Color32[_output.Length];

            for (var row = 0; row < numRows; row++)
            {
                int copyRow;

                // every 8th row starting at 0
                if (row % 8 == 0)
                {
                    copyRow = row / 8;
                }
                // every 8th row starting at 4
                else if ((row + 4) % 8 == 0)
                {
                    var o = numRows / 8;
                    copyRow = o + (row - 4) / 8;
                }
                // every 4th row starting at 2
                else if ((row + 2) % 4 == 0)
                {
                    var o = numRows / 4;
                    copyRow = o + (row - 2) / 4;
                }
                // every 2nd row starting at 1
                else // if( ( r + 1 ) % 2 == 0 )
                {
                    var o = numRows / 2;
                    copyRow = o + (row - 1) / 2;
                }

                Array.Copy(input, (numRows - copyRow - 1) * gifFormat.CanvasWidth, _output, writePos, gifFormat.CanvasWidth);

                writePos -= gifFormat.CanvasWidth;
            }
        }

        private void LZWDecompress(Color32[] colorTable,int width,int height)
        {
            // 图像数据使用一维数组存储
            int row = (height - _imageTop - 1) * width;// y值，gif从左上角开始，unity texture2d从左下角开始
            int col = _imageLeft; //x 的 最小值
            int rightEdge = _imageLeft + _imageWidth; // x的最大值
            //Debug.LogError(col + "   " + rightEdge + "   " + (height - _imageTop));
            int minTableSize = gifData[_byteIndex++]; // 最小/起始 颜色长度(单位为bit)，该值范围为2-8
            //if (minTableSize > 11)
            //    minTableSize = 11;

            // 初始化编译表

            int codeSize = minTableSize + 1;// 单个数字的初始的编码长度，最长不超过12位
            int nextSize = Pow2[codeSize];// 下一次编译表扩容时的最大容量
            int maxCodesLength = Pow2[minTableSize];// 当前编译表中的最大容量（不计下面两个）
            int clearCode = maxCodesLength;// 清除标记
            int endCode = maxCodesLength + 1;// 结束标记
            //Debug.Log("endCode:" + endCode);

            int codesEnd = 0;// 实际code的index
            int codesNum = maxCodesLength + 2;// 编译表长度
            //Debug.Log("Start:" + codesNum);
            //Dictionary<uint, int[]> _dic = new Dictionary<uint, int[]>();
            //for (uint i = 0; i < maxCodesLength; i++)
            //{
            //    _dic.Add(i, new int[1] { (int)i });
            //}

            for (ushort i = 0; i < codesNum; i++)
            {
                _indices[i] = codesEnd;
                _codes[codesEnd++] = 1;// 之后跟着的code个数
                _codes[codesEnd++] = i;// code
            }
            //Debug.Log("_codes end:"+codesEnd+"    " + _codes[codesEnd-1]);
            //LZW解压缩 loop

            uint previousCode = NoCode;// 前缀
            // tip: gif存储颜色数字时采用可变编码（即舍弃高位无用的0），
            uint mask = (uint)(nextSize - 1);//用于提取数字，从低位开始
            uint shiftRegister = 0;//移位缓存器

            int bitsAvailable = 0;// 当前在移位缓存器可读取的bit数
            int bytesAvailable = 0;// 当前Image sub block中剩余的byte数

            int blockPos = 0;

            while (true)
            {
                // 读取数字
                uint curCode = shiftRegister & mask;

                if (bitsAvailable >= codeSize)
                {
                    // 如果剩余的bits可供读取
                    bitsAvailable -= codeSize;
                    shiftRegister >>= codeSize;
                }
                else
                {
                    // 否则，此时curCode中存储了剩余的bits（实际数字的部分bits）
                    // reload shift register


                    // if start of new block
                    // 当前子块已用完，读取下一个子块
                    if (bytesAvailable <= 0)
                    {
                        // read blocksize
                        bytesAvailable = gifData[_byteIndex++];

                        // exit if end of stream
                        if (bytesAvailable == 0)
                        {
                            return;
                        }

                        // read block
                        _curBlock[(bytesAvailable - 1) / 4] = 0; // zero last entry
                        Buffer.BlockCopy(gifData, _byteIndex, _curBlock, 0, bytesAvailable);
                        blockPos = 0;
                        _byteIndex += bytesAvailable;
                    }

                    // load shift register
                    // 刷新缓存器
                    shiftRegister = _curBlock[blockPos++];
                    int newBits = bytesAvailable >= 4 ? 32 : bytesAvailable * 8;
                    bytesAvailable -= 4;

                    // read remaining bits

                    if (bitsAvailable > 0)
                    {
                        // 读取数字时，可能会跨越不同的ImageSubBlock（即可简单理解为ImageData初始是连续的数字流，为切成一个个子块，
                        // 人为插入了子块的大小）
                        // 若curCode中已存储部分bits
                        var bitsRemaining = codeSize - bitsAvailable;
                        curCode |= (shiftRegister << bitsAvailable) & mask;
                        shiftRegister >>= bitsRemaining;
                        bitsAvailable = newBits - bitsRemaining;
                    }
                    else
                    {
                        // 此时curCode为0
                        curCode = shiftRegister & mask;
                        shiftRegister >>= codeSize;
                        bitsAvailable = newBits - codeSize;
                    }
                }

                // process code

                if (curCode == clearCode)
                {
                    // reset codes
                    codeSize = minTableSize + 1;
                    nextSize = Pow2[codeSize];
                    codesNum = maxCodesLength + 2;

                    // reset buffer write pos
                    codesEnd = codesNum * 2;

                    // clear previous code
                    previousCode = NoCode;
                    mask = (uint)(nextSize - 1);

                    continue;
                }
                else if (curCode == endCode)
                {
                    // stop
                    break;
                }

                // 处理数字大致流程如下
                // let CODE be the next code in the code stream
                // is CODE in the code table?
                // Yes:
                //     output { CODE} to index stream
                //     let K be the first index in { CODE}
                //     add { PREVCODE} +K to the code table
                //     set PREVCODE = CODE
                // No:
                //     let K be the first index of { PREVCODE}
                //     output { PREVCODE} +K to index stream
                //     add { PREVCODE} +K to code table
                //     set PREVCODE = CODE

                // 比较可知，两种情况下，均要将{ PREVCODE} +K 填入编译表，只是k的来源不同
                // 均要PREVCODE = CODE，不同点在于“Yes”时只输出{ CODE}到output，而“No”时，还需将k输出到output

                bool plusOne = false;
                int codePos = 0;

                if (curCode < codesNum)
                {
                    // write existing code
                    codePos = _indices[curCode];
                }
                else if (previousCode != NoCode)
                {
                    // write previous code
                    codePos = _indices[previousCode];
                    plusOne = true;//标记需将k输出到output
                }
                else
                {
                    continue;
                }


                // output colours

                var codeLength = _codes[codePos++];
                var newCode = _codes[codePos];

                for (int i = 0; i < codeLength; i++)
                {
                    var code = _codes[codePos++];

                    if (code != _transparentColorIndex && col < width)
                    {
                        _output[row + col] = colorTable[code];
                    }

                    if (++col == rightEdge)
                    {
                        col = _imageLeft;
                        row -= width;

                        if (row < 0)
                        {
                            SkipBlock();
                            return;
                        }
                    }
                }

                if (plusOne)
                {
                    if (newCode != _transparentColorIndex && col < width)
                    {
                        _output[row + col] = colorTable[newCode];
                    }

                    if (++col == rightEdge)
                    {
                        col = _imageLeft;
                        row -= width;

                        if (row < 0)
                        {
                            break;
                        }
                    }
                }

                // create new code

                if (previousCode != NoCode && codesNum != _indices.Length)
                {
                    // get previous code from buffer

                    codePos = _indices[previousCode];
                    codeLength = _codes[codePos++];

                    // resize buffer if required (should be rare)

                    if (codesEnd + codeLength + 1 >= _codes.Length)
                    {
                        Array.Resize(ref _codes, _codes.Length * 2);
                    }

                    // add new code

                    _indices[codesNum++] = codesEnd;
                    _codes[codesEnd++] = (ushort)(codeLength + 1);

                    // copy previous code sequence

                    var stop = codesEnd + codeLength;

                    while (codesEnd < stop)
                    {
                        _codes[codesEnd++] = _codes[codePos++];
                    }

                    // append new code

                    _codes[codesEnd++] = newCode;
                }

                // increase code size?

                if (codesNum >= nextSize && codeSize < 12)
                {
                    nextSize = Pow2[++codeSize];
                    mask = (uint)(nextSize - 1);
                }

                // remember last code processed
                previousCode = curCode;
            }

            SkipBlock();
        }

        private void ReadGraphicControlBlock()
        {

            ReadByte();// block size (0x04)
            var flags = (GraphicControlFlags)ReadByte();
            _delay = ReadUInt16() * 10; // 1/100 s -> milliseconds
            var transparentColorIndex = ReadByte();
            ReadByte(); // block terminator

            if (flags.HasFlag(GraphicControlFlags.TransparentColorFlag))
            {
                _transparentColorIndex = transparentColorIndex;
            }
            else
            {
                _transparentColorIndex = NoTransparency;
            }

            switch((DisposalMethod)(flags & GraphicControlFlags.DisposalMethod))
            {
                case DisposalMethod.None:
                    break;
                case DisposalMethod.DoNotDispose:
                    Debug.Log("DoNotDispose");
                    _previousImage = _output;
                    break;
                case DisposalMethod.RestoreBackground:
                    Debug.Log("RestoreBackground");
                    _output = new Color32[gifFormat.CanvasWidth * gifFormat.CanvasHeight];
                    break;
                case DisposalMethod.ReturnToPrevious:
                    Debug.Log("ReturnToPrevious");
                    _output = new Color32[gifFormat.CanvasWidth * gifFormat.CanvasHeight];
                    if (_previousImage != null)
                    {
                        Array.Copy(_previousImage, _output, _output.Length);
                        _previousImage = null;
                    }
                    break;
            }
        }

        private void SkipBlock()
        {
            var blockSize = gifData[_byteIndex++];
            while(blockSize != 0x00)
            {
                _byteIndex += blockSize;
                blockSize = gifData[_byteIndex++];
            }
        }

        private void ReadHeader()
        {
            var version = Encoding.ASCII.GetString(gifData, 0, 6);
            if (version != "GIF87a" && version != "GIF89a")
            {
                Debug.LogError("Not Gif!");
                return;
            }
            gifFormat.Version = version;
            _byteIndex = 6;

            gifFormat.CanvasWidth = ReadUInt16();
            gifFormat.CanvasHeight = ReadUInt16();

            var flags = (LogicalScreenFlags)ReadByte();
            var bgColorIndex = ReadByte();
            ReadByte();
            if (flags.HasFlag(LogicalScreenFlags.GlobalColorTableFlag))
            {
                int tableSize = Pow2[(int)(flags & LogicalScreenFlags.SizeOfGlobalColorTable) + 1];
                gifFormat.GlobalColorTable = ReadColorTable(tableSize);
                gifFormat.BackgroundColor = gifFormat.GlobalColorTable[bgColorIndex];
            }
            else
            {
                gifFormat.BackgroundColor = Color.black;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // 积极内联
        private byte ReadByte()
        {
            return gifData[_byteIndex++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // 积极内联
        private ushort ReadUInt16()
        {
            // gif 默认小端，即数字以从低到高位存储
            return (ushort)(gifData[_byteIndex++] | gifData[_byteIndex++] << 8);
        }

        private Color32[] ReadColorTable(int tableSize)
        {
            Color32[] colorTable = new Color32[tableSize];
            for (int i = 0; i < tableSize; i++)
            {
                colorTable[i] = new Color32(
                    gifData[_byteIndex++],
                    gifData[_byteIndex++],
                    gifData[_byteIndex++],
                    0xFF
                    );
            }

            return colorTable;
        }

        #endregion 
    }

}