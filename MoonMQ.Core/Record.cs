using System.Diagnostics;
using System.Text.Json;

namespace MoonMQ.Core
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public partial class Record
    {
        public static readonly int Size = 2 * (sizeof(int) + sizeof(long)) + sizeof(int);
        public static byte[] ExtractPayload(byte[] data) => data.Skip(Size).ToArray();

        public static Record Default => new(0, 0, 0, 0, 0);

        public Record(int index, long position, int length, long timestamp, int term)
        {
            Index = index;
            Position = position;
            Length = length;
            Timestamp = timestamp;
            Term = term;
        }

        public int FullLength => Size + Length;

        public Record Metadata => new(Index, Position, Length, Timestamp, Term);

        public bool IsEmpty()
        {
            return Index == 0 &&
                Position == 0 &&
                Length == 0 &&
                Timestamp == 0;
        }

        public byte[] Marshal() => Enumerable.Empty<byte>()
                                             .Concat(BitConverter.GetBytes(Index))
                                             .Concat(BitConverter.GetBytes(Position))
                                             .Concat(BitConverter.GetBytes(Length))
                                             .Concat(BitConverter.GetBytes(Timestamp))
                                             .Concat(BitConverter.GetBytes(Term))
                                             .ToArray();

        public byte[] MarshalWithData() => Marshal().Concat(GetData())
                                                    .ToArray();

        public static Record Unmarshall(byte[] record)
        {
            if (record is null || record.Length < Size) { return new(0, 0, 0, 0, 0); }

            var sizeofint = sizeof(int);
            var sizeoflong = sizeof(long);
            var sizeofuint = sizeof(uint);

            int offset = BitConverter.ToInt32(record.Take(sizeofint)
                                                    .ToArray());
            int position = BitConverter.ToInt32(record.Skip(sizeofint)
                                                      .Take(sizeoflong)
                                                      .ToArray());
            int length = BitConverter.ToInt32(record.Skip(sizeofint)
                                                    .Skip(sizeoflong)
                                                    .Take(sizeofint)
                                                    .ToArray());
            int timestamp = BitConverter.ToInt32(record.Skip(sizeofint)
                                                       .Skip(sizeoflong)
                                                       .Skip(sizeofint)
                                                       .Take(sizeoflong)
                                                       .ToArray());
            int term = BitConverter.ToInt32(record.Skip(sizeofint)
                                                  .Skip(sizeoflong)
                                                  .Skip(sizeofint)
                                                  .Skip(sizeoflong)
                                                  .Take(sizeofuint)
                                                  .ToArray());

            return new Record(offset, position, length, timestamp, term);
        }

        public byte[] GetData()
        {
            return Data.ToArray();
        }

        private string GetDebuggerDisplay() => JsonSerializer.Serialize(this);
    }
}
