﻿using Ohana3DS_Rebirth.Ohana.Models.PICA200;
using Ohana3DS_Rebirth.Ohana.Textures.PocketMonsters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace Ohana3DS_Rebirth.Ohana.Models.PocketMonsters
{
    class GfModel
    {
        /// <summary>
        ///     Loads a Pokémon Sun/Moon Model file.
        /// </summary>
        /// <param name="fileName">File Name of the Model file</param>
        /// <returns></returns>
        public static RenderBase.OModelGroup load(string fileName)
        {
            return load(new FileStream(fileName, FileMode.Open));
        }

        /// <summary>
        ///     Loads a Pokémon Sun/Moon Model  file.
        ///     Note that Model must start at offset 0x0.
        /// </summary>
        /// <param name="data">Stream of the Model file.</param>
        /// <returns></returns>
        public static RenderBase.OModelGroup load(Stream data)
        {
            RenderBase.OModelGroup mdls = new RenderBase.OModelGroup();

            BinaryReader input = new BinaryReader(data);

            input.ReadUInt32();

            uint[] sectionsCnt = new uint[5];

            for (int i = 0; i < 5; i++)
            {
                sectionsCnt[i] = input.ReadUInt32(); //Count for each section on the file (total 5)
            }

            uint baseAddr = (uint)data.Position;

            const int MODEL_SECT = 0;
            const int TEXTURE_SECT = 1;

            for (int sect = 0; sect < 5; sect++)
            {
                uint count = sectionsCnt[sect];

                for (int i = 0; i < count; i++)
                {
                    data.Seek(baseAddr + i * 4, SeekOrigin.Begin);
                    data.Seek(input.ReadUInt32(), SeekOrigin.Begin);

                    byte nameStrLen = input.ReadByte();
                    string name = IOUtils.readStringWithLength(input, nameStrLen);
                    uint descAddress = input.ReadUInt32();

                    data.Seek(descAddress, SeekOrigin.Begin);

                    switch (sect)
                    {
                        case MODEL_SECT:
                            RenderBase.OModel mdl = loadModel(data, true);
                            mdl.name = name;

                            mdls.model.Add(mdl);
                            break;

                        case TEXTURE_SECT: mdls.texture.Add(GfTexture.load(data, true)); break;
                    }
                }

                baseAddr += count * 4;
            }

            data.Close();

            return mdls;
        }

        public static RenderBase.OModel loadModel(Stream data, bool keepOpen = false)
        {
            RenderBase.OModel mdl = new RenderBase.OModel();
            BinaryReader input = new BinaryReader(data);

            mdl.name = "model";

            long mdlStart = data.Position;

            data.Seek(0x10, SeekOrigin.Current);
            ulong mdlMagic = input.ReadUInt64(); //gfmodel string
            uint mdlLength = input.ReadUInt32();
            input.ReadUInt32(); //-1

            string[] effectNames = getStrTable(input);
            string[] textureNames = getStrTable(input);
            string[] materialNames = getStrTable(input);
            string[] meshNames = getStrTable(input);

            input.BaseStream.Seek(0x20, SeekOrigin.Current); //2 float4 (Maybe 2 Quaternions?)

            mdl.transform = new RenderBase.OMatrix();
            mdl.transform.M11 = input.ReadSingle();
            mdl.transform.M12 = input.ReadSingle();
            mdl.transform.M13 = input.ReadSingle();
            mdl.transform.M14 = input.ReadSingle();

            mdl.transform.M21 = input.ReadSingle();
            mdl.transform.M22 = input.ReadSingle();
            mdl.transform.M23 = input.ReadSingle();
            mdl.transform.M24 = input.ReadSingle();

            mdl.transform.M31 = input.ReadSingle();
            mdl.transform.M32 = input.ReadSingle();
            mdl.transform.M33 = input.ReadSingle();
            mdl.transform.M34 = input.ReadSingle();

            mdl.transform.M41 = input.ReadSingle();
            mdl.transform.M42 = input.ReadSingle();
            mdl.transform.M43 = input.ReadSingle();
            mdl.transform.M44 = input.ReadSingle();

            uint unkDataLen = input.ReadUInt32();
            uint unkDataRelStart = input.ReadUInt32();
            input.ReadUInt32();
            input.ReadUInt32();

            input.BaseStream.Seek(unkDataRelStart + unkDataLen, SeekOrigin.Current); //???

            uint bonesCount = input.ReadUInt32();
            input.BaseStream.Seek(0xc, SeekOrigin.Current);

            List<string> boneNames = new List<string>();

            for (int b = 0; b < bonesCount; b++)
            {
                string boneName = IOUtils.readStringWithLength(input, input.ReadByte());
                string parentName = IOUtils.readStringWithLength(input, input.ReadByte());
                byte flags = input.ReadByte();

                RenderBase.OBone bone = new RenderBase.OBone();

                bone.name = boneName;
                bone.parentId = (short)boneNames.IndexOf(parentName);

                bone.scale = new RenderBase.OVector3(
                    input.ReadSingle(),
                    input.ReadSingle(),
                    input.ReadSingle());

                bone.rotation = new RenderBase.OVector3(
                    input.ReadSingle(),
                    input.ReadSingle(),
                    input.ReadSingle());

                bone.translation = new RenderBase.OVector3(
                    input.ReadSingle(),
                    input.ReadSingle(),
                    input.ReadSingle());

                bone.absoluteScale = new RenderBase.OVector3(bone.scale);

                mdl.skeleton.Add(bone);
                boneNames.Add(boneName);
            }

            //Materials
            List<string> matMeshBinding = new List<string>();

            input.BaseStream.Seek(mdlStart + mdlLength + 0x20, SeekOrigin.Begin);

            for (int m = 0; m < materialNames.Length; m++)
            {
                RenderBase.OMaterial mat = new RenderBase.OMaterial();

                mat.name = materialNames[m];

                ulong matMagic = input.ReadUInt64(); //material string
                uint matLength = input.ReadUInt32();
                input.ReadUInt32(); //-1

                long matStart = data.Position;

                string[] unkNames = new string[4];

                for (int n = 0; n < 4; n++)
                {
                    uint maybeHash = input.ReadUInt32();
                    byte nameLen = input.ReadByte();

                    unkNames[n] = IOUtils.readStringWithLength(input, nameLen);
                }

                matMeshBinding.Add(unkNames[0]);

                data.Seek(0xac, SeekOrigin.Current);

                long textureCoordsStart = data.Position;

                for (int unit = 0; unit < 3; unit++)
                {
                    data.Seek(textureCoordsStart + unit * 0x42, SeekOrigin.Begin);

                    uint maybeHash = input.ReadUInt32();
                    string texName = IOUtils.readStringWithLength(input, input.ReadByte());

                    if (texName == string.Empty) break;

                    switch (unit)
                    {
                        case 0: mat.name0 = texName; break;
                        case 1: mat.name1 = texName; break;
                        case 2: mat.name2 = texName; break;
                    }

                    ushort unitIdx = input.ReadUInt16();

                    mat.textureCoordinator[unit].scaleU = input.ReadSingle();
                    mat.textureCoordinator[unit].scaleV = input.ReadSingle();
                    mat.textureCoordinator[unit].rotate = input.ReadSingle();
                    mat.textureCoordinator[unit].translateU = input.ReadSingle();
                    mat.textureCoordinator[unit].translateV = input.ReadSingle();

                    uint texMapperU = input.ReadUInt32();
                    uint texMapperV = input.ReadUInt32();

                    mat.textureMapper[unit].wrapU = (RenderBase.OTextureWrap)(texMapperU & 7);
                    mat.textureMapper[unit].wrapV = (RenderBase.OTextureWrap)(texMapperV & 7);
                }

                mdl.material.Add(mat);

                input.BaseStream.Seek(matStart + matLength, SeekOrigin.Begin);
            }

            //Meshes
            for (int m = 0; m < meshNames.Length; m++)
            {
                ulong meshMagic = input.ReadUInt64(); //mesh string
                uint meshLength = input.ReadUInt32();
                input.ReadUInt32(); //-1

                long meshStart = data.Position;
                //Mesh name and other stuff goes here

                input.BaseStream.Seek(0x80, SeekOrigin.Current);

                subMeshInfo info = getSubMeshInfo(input);

                for (int sm = 0; sm < info.count; sm++)
                {
                    RenderBase.OMesh obj = new RenderBase.OMesh();

                    obj.isVisible = true;
                    obj.name = info.names[sm];
                    obj.materialId = (ushort)matMeshBinding.IndexOf(obj.name);

                    ushort[] nodeList = info.nodeLists[sm];

                    //NOTE: All Addresses on commands are set to 0x99999999 and are probably relocated by game engine
                    PICACommandReader vtxCmdReader = info.cmdBuffers[sm * 3 + 0];
                    PICACommandReader idxCmdReader = info.cmdBuffers[sm * 3 + 2];

                    uint vshAttributesBufferStride = vtxCmdReader.getVSHAttributesBufferStride(0);
                    uint vshTotalAttributes = vtxCmdReader.getVSHTotalAttributes(0);
                    PICACommand.vshAttribute[] vshMainAttributesBufferPermutation = vtxCmdReader.getVSHAttributesBufferPermutation();
                    uint[] vshAttributesBufferPermutation = vtxCmdReader.getVSHAttributesBufferPermutation(0);
                    PICACommand.attributeFormat[] vshAttributesBufferFormat = vtxCmdReader.getVSHAttributesBufferFormat();

                    for (int attribute = 0; attribute < vshTotalAttributes; attribute++)
                    {
                        switch (vshMainAttributesBufferPermutation[vshAttributesBufferPermutation[attribute]])
                        {
                            case PICACommand.vshAttribute.normal: obj.hasNormal = true; break;
                            case PICACommand.vshAttribute.tangent: obj.hasTangent = true; break;
                            case PICACommand.vshAttribute.color: obj.hasColor = true; break;
                            case PICACommand.vshAttribute.textureCoordinate0: obj.texUVCount = Math.Max(obj.texUVCount, 1); break;
                            case PICACommand.vshAttribute.textureCoordinate1: obj.texUVCount = Math.Max(obj.texUVCount, 2); break;
                            case PICACommand.vshAttribute.textureCoordinate2: obj.texUVCount = Math.Max(obj.texUVCount, 3); break;
                        }
                    }

                    PICACommand.indexBufferFormat idxBufferFormat = idxCmdReader.getIndexBufferFormat();
                    uint idxBufferTotalVertices = idxCmdReader.getIndexBufferTotalVertices();

                    obj.hasNode = true;
                    obj.hasWeight = true;

                    long vtxBufferStart = data.Position;

                    input.BaseStream.Seek(info.vtxLengths[sm], SeekOrigin.Current);

                    long idxBufferStart = data.Position;

                    for (int faceIndex = 0; faceIndex < idxBufferTotalVertices; faceIndex++)
                    {
                        ushort index = 0;

                        switch (idxBufferFormat)
                        {
                            case PICACommand.indexBufferFormat.unsignedShort: index = input.ReadUInt16(); break;
                            case PICACommand.indexBufferFormat.unsignedByte: index = input.ReadByte(); break;
                        }

                        long dataPosition = data.Position;
                        long vertexOffset = vtxBufferStart + (index * vshAttributesBufferStride);
                        data.Seek(vertexOffset, SeekOrigin.Begin);

                        RenderBase.OVertex vertex = new RenderBase.OVertex();
                        vertex.diffuseColor = 0xffffffff;
                        // Fix weight problems
                        vertex.weight.Add(1);
                        vertex.weight.Add(0);
                        vertex.weight.Add(0);
                        vertex.weight.Add(0);

                        for (int attribute = 0; attribute < vshTotalAttributes; attribute++)
                        {
                            //gdkchan self note: The Attribute type flags are used for something else on Bone Weight (and bone index?)
                            PICACommand.vshAttribute att = vshMainAttributesBufferPermutation[vshAttributesBufferPermutation[attribute]];
                            PICACommand.attributeFormat format = vshAttributesBufferFormat[vshAttributesBufferPermutation[attribute]];
                            if (att == PICACommand.vshAttribute.boneWeight) format.type = PICACommand.attributeFormatType.unsignedByte;
                            RenderBase.OVector4 vector = getVector(input, format);

                            switch (att)
                            {
                                case PICACommand.vshAttribute.position:
                                    vertex.position = new RenderBase.OVector3(vector.x, vector.y, vector.z);
                                    break;
                                case PICACommand.vshAttribute.normal:
                                    vertex.normal = new RenderBase.OVector3(vector.x, vector.y, vector.z);
                                    break;
                                case PICACommand.vshAttribute.tangent:
                                    vertex.tangent = new RenderBase.OVector3(vector.x, vector.y, vector.z);
                                    break;
                                case PICACommand.vshAttribute.color:
                                    uint r = MeshUtils.saturate(vector.x);
                                    uint g = MeshUtils.saturate(vector.y);
                                    uint b = MeshUtils.saturate(vector.z);
                                    uint a = MeshUtils.saturate(vector.w);
                                    vertex.diffuseColor = b | (g << 8) | (r << 16) | (a << 24);
                                    break;
                                case PICACommand.vshAttribute.textureCoordinate0:
                                    vertex.texture0 = new RenderBase.OVector2(vector.x, vector.y);
                                    break;
                                case PICACommand.vshAttribute.textureCoordinate1:
                                    vertex.texture1 = new RenderBase.OVector2(vector.x, vector.y);
                                    break;
                                case PICACommand.vshAttribute.textureCoordinate2:
                                    vertex.texture2 = new RenderBase.OVector2(vector.x, vector.y);
                                    break;
                                case PICACommand.vshAttribute.boneIndex:
                                    addNode(vertex.node, nodeList, (int)vector.x);
                                    if (format.attributeLength > 0) addNode(vertex.node, nodeList, (int)vector.y);
                                    if (format.attributeLength > 1) addNode(vertex.node, nodeList, (int)vector.z);
                                    if (format.attributeLength > 2) addNode(vertex.node, nodeList, (int)vector.w);
                                    break;
                                case PICACommand.vshAttribute.boneWeight:
                                    vertex.weight[0] = (vector.x / 255f);
                                    if (format.attributeLength > 0) vertex.weight[1] = (vector.y / 255f);
                                    if (format.attributeLength > 1) vertex.weight[2] = (vector.z / 255f);
                                    if (format.attributeLength > 2) vertex.weight[3] = (vector.w / 255f);
                                    break;
                            }
                        }

                        //If the node list have 4 or less bones, then there is no need to store the indices per vertex
                        //Instead, the entire list is used, since it supports up to 4 bones.
                        if (vertex.node.Count == 0 && nodeList.Length <= 4)
                        {
                            for (int n = 0; n < nodeList.Length; n++) vertex.node.Add(nodeList[n]);
                            if (vertex.weight.Count == 0) vertex.weight.Add(1);
                        }

                        MeshUtils.calculateBounds(mdl, vertex);
                        obj.vertices.Add(vertex);

                        data.Seek(dataPosition, SeekOrigin.Begin);
                    }

                    input.BaseStream.Seek(idxBufferStart + info.idxLengths[sm], SeekOrigin.Begin);

                    mdl.mesh.Add(obj);
                }

                input.BaseStream.Seek(meshStart + meshLength, SeekOrigin.Begin);
            }

            if (!keepOpen) data.Close();

            return mdl;
        }

        private static RenderBase.OVector4 getVector(BinaryReader input, PICACommand.attributeFormat format)
        {
            RenderBase.OVector4 output = new RenderBase.OVector4();

            switch (format.type)
            {
                case PICACommand.attributeFormatType.signedByte:
                    output.x = (sbyte)input.ReadByte();
                    if (format.attributeLength > 0) output.y = (sbyte)input.ReadByte();
                    if (format.attributeLength > 1) output.z = (sbyte)input.ReadByte();
                    if (format.attributeLength > 2) output.w = (sbyte)input.ReadByte();
                    break;
                case PICACommand.attributeFormatType.unsignedByte:
                    output.x = input.ReadByte();
                    if (format.attributeLength > 0) output.y = input.ReadByte();
                    if (format.attributeLength > 1) output.z = input.ReadByte();
                    if (format.attributeLength > 2) output.w = input.ReadByte();
                    break;
                case PICACommand.attributeFormatType.signedShort:
                    output.x = input.ReadInt16();
                    if (format.attributeLength > 0) output.y = input.ReadInt16();
                    if (format.attributeLength > 1) output.z = input.ReadInt16();
                    if (format.attributeLength > 2) output.w = input.ReadInt16();
                    break;
                case PICACommand.attributeFormatType.single:
                    output.x = input.ReadSingle();
                    if (format.attributeLength > 0) output.y = input.ReadSingle();
                    if (format.attributeLength > 1) output.z = input.ReadSingle();
                    if (format.attributeLength > 2) output.w = input.ReadSingle();
                    break;
            }

            return output;
        }

        private static string[] getStrTable(BinaryReader input)
        {
            uint count = input.ReadUInt32();
            uint baseAddress = (uint)input.BaseStream.Position;

            string[] output = new string[count];

            for (int i = 0; i < count; i++)
            {
                input.BaseStream.Seek(baseAddress + i * 0x44, SeekOrigin.Begin);

                uint maybeHash = input.ReadUInt32();
                output[i] = IOUtils.readStringWithLength(input, 0x40);
            }

            input.BaseStream.Seek(baseAddress + count * 0x44, SeekOrigin.Begin);

            return output;
        }

        private struct subMeshInfo
        {
            public List<PICACommandReader> cmdBuffers;
            public List<ushort[]> nodeLists;
            public List<uint> vtxLengths;
            public List<uint> idxLengths;
            public List<string> names;

            public int count;
        }

        private static subMeshInfo getSubMeshInfo(BinaryReader input)
        {
            subMeshInfo output = new subMeshInfo();

            output.cmdBuffers = new List<PICACommandReader>();
            output.nodeLists = new List<ushort[]>();
            output.vtxLengths = new List<uint>();
            output.idxLengths = new List<uint>();
            output.names = new List<string>();

            int currCmdIdx = 0;
            int totalCmds = 0;

            while ((currCmdIdx + 1 < totalCmds) || currCmdIdx == 0)
            {
                uint cmdLength = input.ReadUInt32();
                currCmdIdx = input.ReadInt32();
                totalCmds = input.ReadInt32();
                input.ReadInt32();

                output.cmdBuffers.Add(new PICACommandReader(input.BaseStream, cmdLength / 4));
            }

            output.count = totalCmds / 3;

            for (int i = 0; i < output.count; i++)
            {
                uint maybeHash = input.ReadUInt32();
                uint subMeshNameLen = input.ReadUInt32();
                long subMeshNameStart = input.BaseStream.Position;
                string name = IOUtils.readStringWithLength(input, subMeshNameLen);

                input.BaseStream.Seek(subMeshNameStart + subMeshNameLen, SeekOrigin.Begin);

                long nodeListStart = input.BaseStream.Position;
                byte nodeListLen = input.ReadByte();
                ushort[] nodeList = new ushort[nodeListLen];
                for (int n = 0; n < nodeListLen; n++) nodeList[n] = input.ReadByte();

                input.BaseStream.Seek(nodeListStart + 0x20, SeekOrigin.Begin);

                uint vtxCount = input.ReadUInt32();
                uint idxCount = input.ReadUInt32();
                output.vtxLengths.Add(input.ReadUInt32());
                output.idxLengths.Add(input.ReadUInt32());

                output.names.Add(name);
                output.nodeLists.Add(nodeList);
            }

            return output;
        }

        private static void addNode(List<int> target, ushort[] nodeList, int nodeVal)
        {
            if (nodeVal != 0xff) target.Add(nodeList[nodeVal]);
        }
    }
}
