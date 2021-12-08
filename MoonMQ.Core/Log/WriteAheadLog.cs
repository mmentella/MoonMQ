namespace MoonMQ.Core.Log
{
    class WriteAheadLog 
        : IDisposable
    {
        private static readonly DateTime UnixEpoch = new(1970, 1, 1);

        private bool disposed;

        private readonly Dictionary<int, Record> offsetMap;

        private readonly BinaryReader reader;
        private readonly BinaryWriter writer;

        private readonly BinaryReader mapreader;
        private readonly BinaryWriter mapwriter;

        public WriteAheadLog(string log)
        {
            _ = log ?? throw new ArgumentNullException(nameof(log));
            var maplog = $"{log}.map";

            mapreader = new BinaryReader(new FileStream(maplog, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Write, 0, FileOptions.Asynchronous));
            mapwriter = new BinaryWriter(new FileStream(maplog, FileMode.Append, FileAccess.Write, FileShare.Read, 0, options: FileOptions.WriteThrough | FileOptions.Asynchronous));

            reader = new BinaryReader(new FileStream(log, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Write, 0, FileOptions.Asynchronous));
            writer = new BinaryWriter(new FileStream(log, FileMode.Append, FileAccess.Write, FileShare.Read, 0, options: FileOptions.WriteThrough | FileOptions.Asynchronous));

            offsetMap = new Dictionary<int, Record>();
            LoadOffsetMap();
        }

        public Record Push(byte[] data, int term)
        {
            if (data is null || !data.Any()) return new(0, 0, 0, 0, 0);

            Record record = new(offsetMap.Count + 1, writer.BaseStream.Position, data.Length, Timestamp(), term);
            writer.Write(record.Marshal().Concat(data).ToArray());
            writer.Flush();

            mapwriter.Write(record.Marshal());
            mapwriter.Flush();

            offsetMap.Add(offsetMap.Count + 1, record);

            return record;
        }

        public byte[] Pull(int offset)
        {
            if (!offsetMap.TryGetValue(offset, out Record record)) { return Array.Empty<byte>(); }

            reader.BaseStream.Seek(record.Position, SeekOrigin.Begin);
            return Record.ExtractPayload(reader.ReadBytes(record.FullLength));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            reader.Close();
            reader.Dispose();

            writer.Flush();
            writer.Close();
            writer.Dispose();

            mapreader.Close();
            mapreader.Dispose();

            mapwriter.Flush();
            mapwriter.Close();
            mapwriter.Dispose();

            disposed = true;
        }

        private void LoadOffsetMap()
        {
            mapreader.BaseStream.Seek(0, SeekOrigin.Begin);
            byte[] byterecord = new byte[Record.Size];
            Record record;

            int byteRead;
            while ((byteRead = mapreader.Read(byterecord, 0, Record.Size)) > 0)
            {
                if (byteRead < Record.Size) { continue; }

                record = Record.Unmarshall(byterecord);
                if (record.IsNull()) { continue; }

                offsetMap.Add(record.Index, record);
            }
        }

        private static long Timestamp() => (long)(DateTime.Now - UnixEpoch).TotalSeconds;
    }
}
