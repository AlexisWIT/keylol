﻿using System.IO;

namespace Keylol.FontGarage.Table
{
    public class BinaryDataTable : IOpenTypeFontTable
    {
        public byte[] Data { get; set; }
        public string Tag { get; set; }

        public void Serialize(BinaryWriter writer, long startOffset, SerializationInfo additionalInfo)
        {
            writer.BaseStream.Position = startOffset;
            writer.Write(Data);
        }

        public object DeepCopy()
        {
            var newTable = (BinaryDataTable) MemberwiseClone();
            newTable.Data = (byte[]) Data.Clone();
            return newTable;
        }

        public static BinaryDataTable Deserialize(BinaryReader reader, long startOffset, uint length, string tag)
        {
            reader.BaseStream.Position = startOffset;
            return new BinaryDataTable
            {
                Tag = tag,
                Data = reader.ReadBytes((int) length)
            };
        }
    }
}