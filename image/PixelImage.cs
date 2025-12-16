// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;

namespace g3
{
#nullable enable

    public class PixelImage
    {
        public enum EPixelFormat
        {
            RGBA8 = 0,
            RGBAFloat32 = 1,

            Encoded = 77
        }
        public enum ECompression
        {
            Uncompressed = 0,

            PNG = 10,
            JPEG = 11,

            UnspecifiedImageFormat = 99
        };

        public EPixelFormat Format { get; protected set; } = EPixelFormat.RGBA8;
        public ECompression Compression { get; set; } = ECompression.Uncompressed;

        public int Width { get; protected set; } = 0;
        public int Height { get; protected set; } = 0;
        public int Channels { get; protected set; } = 4;

        byte[] data = null!;        // could this be Span<byte> ?

        public PixelImage(int width, int height, EPixelFormat format)
        {
            initialize(width, height, format);
        }
        public PixelImage(int width, int height, EPixelFormat format, byte[] imageBuffer)
        {
            initialize(width, height, format, imageBuffer);
        }

        public PixelImage(int width, int height, Span<byte> data, EPixelFormat format, ECompression compression)
        {
            if (compression == ECompression.Uncompressed) {
                initialize(width, height, format);
                data.CopyTo(this.data);
            } else {
                this.Width = width;
                this.Height = height;
                this.Channels = ChannelCount(format);
                this.Format = format;
                this.Compression = compression;
                this.data = data.ToArray();
            }
        }



        public bool IsValid {
            get { return data != null && data.Length > 0 && (Width*Height) > 0; }
        }


        public ReadOnlySpan<byte> AccessDataUnsafe() {
            return data.AsSpan();
        }

        public void ProcessImage(Action<byte[]> ProcessFunc)
        {
            ProcessFunc(data);
        }

        public byte[] ExtractBytes(bool bCreateCopy = true)
        {
            if (bCreateCopy) {
                byte[] newData = new byte[data.Length];
                Array.Copy(data, newData, data.Length);
                return newData;
            }
            byte[] returnData = data;
            initialize(1, 1, Format);
            return returnData;
        }


        public void UpdateFromBytes(byte[] NewRGBABytes, bool bTakeOwnership = false)
        {
            if (Format != EPixelFormat.RGBA8 || Compression != ECompression.Uncompressed )
                throw new Exception("[PixelImage.UpdateFromBytes] image must be Uncompressed RGBA8");
            if (Width*Height*4 != NewRGBABytes.Length)
                throw new Exception("[PixelImage.UpdateFromBytes] Input byte array is of the incorrect length");
            if (bTakeOwnership)
                this.data = NewRGBABytes;
            else
                Array.Copy(NewRGBABytes, this.data, NewRGBABytes.Length);
        }


        public static int ChannelCount(EPixelFormat format)
        {
            switch (format) {
                case EPixelFormat.RGBA8:
                case EPixelFormat.RGBAFloat32:
                    return 4;
                case EPixelFormat.Encoded: 
                    return 1;
                default:
                    throw new Exception("PixelImage.NumChannels(): unrecognized format " + format.ToString());
            }
        }
        public static int ChannelByteSize(EPixelFormat format)
        {
            switch (format) {
                case EPixelFormat.RGBA8:             return 1;
                case EPixelFormat.RGBAFloat32:       return 4;

                case EPixelFormat.Encoded:           return 1;
                default:
                    throw new Exception("PixelImage.ChannelByteSize(): unrecognized format " + format.ToString());
            }
        }


        protected void initialize(int width, int height, EPixelFormat format, byte[]? useData = null)
        {
            this.Width = width;
            this.Height = height;
            this.Compression = ECompression.Uncompressed;

            this.Channels = ChannelCount(format);
            int ChannelBytes = ChannelByteSize(format);

            this.Format = format;

            int NumBytes = width * height * Channels * ChannelBytes;
            if (useData == null) {
                data = new byte[NumBytes];
            } else {
                if ( useData.Length != NumBytes )
                    throw new Exception("PixelImage.initialize(): provided byte buffer has incorrect length");
                data = useData;
            }
        }

    }

#nullable disable
}
