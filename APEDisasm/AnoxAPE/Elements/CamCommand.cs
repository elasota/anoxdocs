using System.IO;
using System;

namespace AnoxAPE.Elements
{
    public class CamCommand : IWindowCommand
    {
        public ByteString Name { get; private set; }
        public OptionalString From { get; private set; }
        public OptionalString To { get; private set; }
        public OptionalString Owner { get; private set; }
        public ushort Yaw { get; private set; }
        public ushort Pitch { get; private set; }
        public ushort Fov { get; private set; }
        public ushort Far { get; private set; }
        public ushort Near { get; private set; }
        public ushort Fwd { get; private set; }
        public ushort Speed { get; private set; }
        public ushort Lift { get; private set; }
        public ushort Lag { get; private set; }
        public ushort Occlude { get; private set; }
        public ushort Restore { get; private set; }
        public ushort Zip { get; private set; }

        public static ushort UnsetValue { get { return 0x8001; } }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Cam; } }

        public CamCommand()
        {
            Name = new ByteString();
            From = new OptionalString();
            To = new OptionalString();
            Owner = new OptionalString();
        }

        public CamCommand(ByteString name, OptionalString from, OptionalString to, OptionalString owner, ushort yaw, ushort pitch, ushort fov, ushort far, ushort near, ushort fwd, ushort speed, ushort lift, ushort lag, ushort occlude, ushort restore, ushort zip)
        {
            Name = name;
            From = from;
            To = to;
            Owner = owner;
            Yaw = yaw;
            Pitch = pitch;
            Fov = fov;
            Far = far;
            Near = near;
            Fwd = fwd;
            Speed = speed;
            Lift = lift;
            Lag = lag;
            Occlude = occlude;
            Restore = restore;
            Zip = zip;
        }


        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "CamCommand");

            Name.Load(inStream, indent + 1, disasmStream);
            From.Load(inStream, indent + 1, disasmStream);
            To.Load(inStream, indent + 1, disasmStream);
            Owner.Load(inStream, indent + 1, disasmStream);

            Yaw = inStream.ReadUInt16();
            Pitch = inStream.ReadUInt16();
            Fov = inStream.ReadUInt16();
            Far = inStream.ReadUInt16();
            Near = inStream.ReadUInt16();
            Fwd = inStream.ReadUInt16();
            Speed = inStream.ReadUInt16();
            Lift = inStream.ReadUInt16();
            Lag = inStream.ReadUInt16();
            Occlude = inStream.ReadUInt16();
            Restore = inStream.ReadUInt16();
            Zip = inStream.ReadUInt16();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent + 1, $"CamParams(Yaw={Yaw},Pitch={Pitch},Fov={Fov},Far={Far},Near={Near},Fwd={Fwd},Speed={Speed},Lift={Lift},Lag={Lag},Occlude={Occlude},Restore={Restore},Zip={Zip})");
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(77);
            Name.Write(outStream);
            From.Write(outStream);
            To.Write(outStream);
            Owner.Write(outStream);

            outStream.WriteUInt16(Yaw);
            outStream.WriteUInt16(Pitch);
            outStream.WriteUInt16(Fov);
            outStream.WriteUInt16(Far);
            outStream.WriteUInt16(Near);
            outStream.WriteUInt16(Fwd);
            outStream.WriteUInt16(Speed);
            outStream.WriteUInt16(Lift);
            outStream.WriteUInt16(Lag);
            outStream.WriteUInt16(Occlude);
            outStream.WriteUInt16(Restore);
            outStream.WriteUInt16(Zip);
        }
    }
}
