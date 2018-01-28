using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Ohana3DS_Rebirth.Ohana.Models;
using Ohana3DS_Rebirth.Ohana.Models.GenericFormats;
using Ohana3DS_Rebirth.Ohana.Models.PocketMonsters;
using Ohana3DS_Rebirth.Ohana.Textures.PocketMonsters;
using Ohana3DS_Rebirth.Ohana.Animations.PocketMonsters;
using Ohana3DS_Rebirth.Ohana.Textures;
using Ohana3DS_Rebirth.Ohana.Compressions;
using Ohana3DS_Rebirth.Ohana.Containers;
using Ohana3DS_Rebirth.Ohana.Animations;

namespace Ohana3DS_Rebirth.Ohana
{
    public class FileIO
    {
        [Flags]
        public enum formatType : uint
        {
            unsupported = 0,
            compression = 1 << 0,
            container = 1 << 1,
            image = 1 << 2,
            model = 1 << 3,
            texture = 1 << 4,
            anims = 1 << 5,
            all = 0xffffffff
        }

        public struct file
        {
            public object data;
            public formatType type;
        }

        public static file load(string fileName)
        {
            switch (Path.GetExtension(fileName).ToLower())
            {
                case ".mbn": return new file { data = MBN.load(fileName), type = formatType.model };
                case ".xml": return new file { data = NLP.load(fileName), type = formatType.model };
                default: return load(new FileStream(fileName, FileMode.Open));
            }
        }

        public static file load(Stream data)
        {
            //Too small
            if (data.Length < 0x10)
            {
                data.Close();
                return new file { type = formatType.unsupported };
            }

            BinaryReader input = new BinaryReader(data);
            uint magic, length;

            switch (peek(input))
            {
                case 0x00010000: return new file { data = GfModel.load(data), type = formatType.model };
                case 0x00060000: return new file { data = GfMotion.loadAnim(input), type = formatType.anims };
                case 0x15041213: return new file { data = GfTexture.load(data), type = formatType.image };
                case 0x15122117:
                    RenderBase.OModelGroup mdls = new RenderBase.OModelGroup();
                    mdls.model.Add(GfModel.loadModel(data));
                    return new file { data = mdls, type = formatType.model };
            }

            switch (getMagic(input, 5))
            {
                case "MODEL": return new file { data = DQVIIPack.load(data), type = formatType.container };
            }

            switch (getMagic(input, 4))
            {
                case "CGFX": return new file { data = CGFX.load(data), type = formatType.model };
                case "CRAG": return new file { data = GARC.load(data), type = formatType.container };
                case "darc": return new file { data = DARC.load(data), type = formatType.container };
                case "FPT0": return new file { data = FPT0.load(data), type = formatType.container };
                case "IECP":
                    magic = input.ReadUInt32();
                    length = input.ReadUInt32();
                    return load(new MemoryStream(LZSS.decompress(data, length)));
                case "NLK2":
                    data.Seek(0x80, SeekOrigin.Begin);
                    return new file
                    {
                        data = CGFX.load(data),
                        type = formatType.model
                    };
                case "SARC": return new file { data = SARC.load(data), type = formatType.container };
                case "SMES": return new file { data = NLP.loadMesh(data), type = formatType.model };
                case "Yaz0":
                    magic = input.ReadUInt32();
                    length = IOUtils.endianSwap(input.ReadUInt32());
                    data.Seek(8, SeekOrigin.Current);
                    return load(new MemoryStream(Yaz0.decompress(data, length)));
                case "zmdl": return new file { data = ZMDL.load(data), type = formatType.model };
                case "ztex": return new file { data = ZTEX.load(data), type = formatType.texture };
            }

            //Check if is a BCLIM or BFLIM file (header on the end)
            if (data.Length > 0x28)
            {
                data.Seek(-0x28, SeekOrigin.End);
                string clim = IOUtils.readStringWithLength(input, 4);
                if (clim == "CLIM" || clim == "FLIM") return new file { data = BCLIM.load(data), type = formatType.image };
            }

            switch (getMagic(input, 3))
            {
                case "BCH":
                    byte[] buffer = new byte[data.Length];
                    input.Read(buffer, 0, buffer.Length);
                    data.Close();
                    return new file
                    {
                        data = BCH.load(new MemoryStream(buffer)),
                        type = formatType.model
                    };
                case "DMP": return new file { data = DMP.load(data), type = formatType.image };
            }

            string magic2b = getMagic(input, 2);

            switch (magic2b)
            {
                case "AD": return new file { data = AD.load(data), type = formatType.model };
                case "BM": return new file { data = MM.load(data), type = formatType.model };
                case "BS": return new file { data = BS.load(data), type = formatType.anims };
                case "CM": return new file { data = CM.load(data), type = formatType.model };
                case "CP": return new file { data = CP.load(data), type = formatType.model };
                case "GR": return new file { data = GR.load(data), type = formatType.model };
                case "MM": return new file { data = MM.load(data), type = formatType.model };
                case "PC": return new file { data = PC.load(data), type = formatType.model };
                case "PT": return new file { data = PT.load(data), type = formatType.texture };
            }

            if (magic2b.Length == 2)
            {
                if ((magic2b[0] >= 'A' && magic2b[0] <= 'Z') &&
                    (magic2b[1] >= 'A' && magic2b[1] <= 'Z'))
                {
                    return new file { data = PkmnContainer.load(data), type = formatType.container };
                }
            }

            //Compressions
            data.Seek(0, SeekOrigin.Begin);
            uint cmp = input.ReadUInt32();
            if ((cmp & 0xff) == 0x13) cmp = input.ReadUInt32();
            switch (cmp & 0xff)
            {
                case 0x11: return load(new MemoryStream(LZSS_Ninty.decompress(data, cmp >> 8)));
                case 0x90:
                    byte[] buffer = BLZ.decompress(data);
                    byte[] newData = new byte[buffer.Length - 1];
                    Buffer.BlockCopy(buffer, 1, newData, 0, newData.Length);
                    return load(new MemoryStream(newData));
            }

            data.Close();
            return new file { type = formatType.unsupported };
        }

        public static string getExtension(byte[] data, int startIndex = 0)
        {
            if (data.Length > 3 + startIndex)
            {
                switch (getMagic(data, 4, startIndex))
                {
                    case "CGFX": return ".bcres";
                }
            }

            if (data.Length > 2 + startIndex)
            {
                switch (getMagic(data, 3, startIndex))
                {
                    case "BCH": return ".bch";
                }
            }

            if (data.Length > 1 + startIndex)
            {
                switch (getMagic(data, 2, startIndex))
                {
                    case "AD": return ".ad";
                    case "BG": return ".bg";
                    case "BM": return ".bm";
                    case "BS": return ".bs";
                    case "CM": return ".cm";
                    case "GR": return ".gr";
                    case "MM": return ".mm";
                    case "PB": return ".pb";
                    case "PC": return ".pc";
                    case "PF": return ".pf";
                    case "PK": return ".pk";
                    case "PO": return ".po";
                    case "PT": return ".pt";
                    case "TM": return ".tm";
                }
            }

            return ".bin";
        }

        private static uint peek(BinaryReader input)
        {
            uint value = input.ReadUInt32();
            input.BaseStream.Seek(-4, SeekOrigin.Current);
            return value;
        }

        private static string getMagic(BinaryReader input, uint length)
        {
            string magic = IOUtils.readString(input, 0, length);
            input.BaseStream.Seek(0, SeekOrigin.Begin);
            return magic;
        }

        public static string getMagic(byte[] data, int length, int startIndex = 0)
        {
            return Encoding.ASCII.GetString(data, startIndex, length);
        }

        public enum fileType
        {
            none,
            model,
            texture,
            container,
            skeletalAnimation,
            materialAnimation,
            visibilityAnimation
        }

        public static FileIO.fileType strToFileType(string fileType)
        {
            switch (fileType.ToLower())
            {
                case "model":
                    return FileIO.fileType.model;
                case "texture":
                    return FileIO.fileType.texture;
                case "container":
                    return FileIO.fileType.container;
                case "skeletalAnimation":
                    return FileIO.fileType.skeletalAnimation;
                case "materialAnimation":
                    return FileIO.fileType.materialAnimation;
                case "visibilityAnimation":
                    return FileIO.fileType.visibilityAnimation;
            }
            return FileIO.fileType.none;
        }

        /// <summary>
        ///     Imports a file of the given type.
        ///     Returns data relative to the chosen type.
        /// </summary>
        /// <param name="type">The type of the data</param>
        /// <returns></returns>
        public static object import(string fileName)
        {
            try
            {
                FileIO.file file = FileIO.load(fileName);
                FileIO.formatType currentFormat = file.type;

                if (currentFormat != FileIO.formatType.unsupported)
                {
                    switch (currentFormat)
                    {
                        case FileIO.formatType.container:
                            ((OContainer)file.data).name = Path.GetFileNameWithoutExtension(fileName);
                            return (OContainer)file.data;
                        case FileIO.formatType.image:
                            ((RenderBase.OTexture)file.data).name = Path.GetFileNameWithoutExtension(fileName);
                            return (RenderBase.OTexture)file.data;
                        case FileIO.formatType.model:
                            return (RenderBase.OModelGroup)file.data;
                        case FileIO.formatType.texture:
                            return (RenderBase.OTexture)file.data;
                    }
                }
                else
                    Console.WriteLine("Error: Unsupported file format!");

            }
            catch (Exception)
            {
                Console.WriteLine("Error: Unsupported file format!");
            }
            return null;
        }

        /// <summary>
        ///     Exports a file of a given type.
        ///     Formats available to export will depend on the type of the data.
        /// </summary>
        /// <param name="type">Type of the data to be exported</param>
        /// <param name="data">The data</param>
        /// <param name="arguments">Optional arguments to be used by the exporter</param>
        public static void export(fileType type, object data, params int[] arguments)
        {
            string fileName = null;

            switch (type)
            {
                case fileType.container:
                    foreach (OContainer.fileEntry file in ((OContainer)data).content)
                    {
                        Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\" + ((OContainer)data).name);

                        fileName = Path.Combine(System.Environment.CurrentDirectory + "\\" + ((OContainer)data).name, file.name);
                        string dir = Path.GetDirectoryName(fileName);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                        byte[] buffer;

                        if (file.loadFromDisk)
                        {
                            buffer = new byte[file.fileLength];
                            ((OContainer)data).data.Seek(file.fileOffset, SeekOrigin.Begin);
                            ((OContainer)data).data.Read(buffer, 0, buffer.Length);
                        }
                        else
                            buffer = file.data;

                        if (file.doDecompression) buffer = LZSS_Ninty.decompress(buffer);

                        File.WriteAllBytes(fileName, buffer);
                        Console.WriteLine("Extracted file: " + fileName);
                    }
                    break;
                case fileType.model:
                    for (int i = 0; i < ((RenderBase.OModelGroup)data).model.Count; i++)
                    {
                        fileName = Path.Combine(System.Environment.CurrentDirectory, ((RenderBase.OModelGroup)data).model[i].name);

                        switch (arguments[0])
                        {
                            case 0: DAE.export(((RenderBase.OModelGroup)data), fileName + ".dae", i); break;
                            case 1: SMD.export(((RenderBase.OModelGroup)data), fileName + ".smd", i); break;
                            case 2: OBJ.export(((RenderBase.OModelGroup)data), fileName + ".obj", i); break;
                            case 3: CMDL.export(((RenderBase.OModelGroup)data), fileName + ".cmdl", i); break;
                        }

                        Console.WriteLine("Extracted file: " + fileName);
                    }
                    break;
                case fileType.texture:
                    if (data.GetType().Equals(typeof(RenderBase.OModelGroup))) { //if extracting textures from a model
                        Directory.CreateDirectory(System.Environment.CurrentDirectory + "\\" + ((RenderBase.OModelGroup)data).model[0].name + "_tex");

                        foreach (RenderBase.OTexture tex in ((RenderBase.OModelGroup)data).texture)
                        {
                            fileName = Path.Combine(System.Environment.CurrentDirectory + "\\" + ((RenderBase.OModelGroup)data).model[0].name + "_tex", tex.name + ".png");
                            tex.texture.Save(fileName);
                            Console.WriteLine("Extracted file: " + fileName);
                        }
                    }
                    else // not a model
                    {
                        fileName = Path.Combine(System.Environment.CurrentDirectory, ((RenderBase.OTexture)data).name + ".png");
                        ((RenderBase.OTexture)data).texture.Save(fileName);
                        Console.WriteLine("Extracted file: " + fileName);
                    }
                    break;
                case fileType.skeletalAnimation:
                    fileName = Path.Combine(System.Environment.CurrentDirectory, ((RenderBase.OModelGroup)data).model[0].name);
                    SMD.export((RenderBase.OModelGroup)data, fileName, arguments[0], arguments[1]);
                    Console.WriteLine("Extracted file: " + fileName);
                    break;
            }
            Console.WriteLine("Extracting files completed!");
        }
    }
}
