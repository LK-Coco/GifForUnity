using System.Collections;
using UnityEngine;


    public static class GifExMethod
    {
        public static int ToNumeral(this BitArray array)
        {
            if (array == null)
            {
                Debug.LogError("array is nothing.");
                return 0;
            }
            if (array.Length > 32)
            {
                Debug.LogError("must be at most 32 bits long.");
                return 0;
            }

            var result = new int[1];
            array.CopyTo(result, 0);
            return result[0];
        }

        public static int GetNumeral(this BitArray array, int startIndex, int bitLength)
        {
            var newArray = new BitArray(bitLength);

            for (int i = 0; i < bitLength; i++)
            {
                if (array.Length <= startIndex + i)
                {
                    newArray[i] = false;
                }
                else
                {
                    bool bit = array.Get(startIndex + i);
                    newArray[i] = bit;
                }
            }

            return newArray.ToNumeral();
        }
    }

