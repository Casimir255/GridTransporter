using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.ObjectBuilders;

namespace GridTransporter.Utilities
{
    public static class EntityBase
    {
        public static byte[] SerializeOB<T>(T instance) where T : MyObjectBuilder_Base
        {
            if (instance == null)
                return null;

            using (var m = new MemoryStream())
            {
                MyObjectBuilderSerializer.SerializePB(m, instance);
                return m.ToArray();
            }
        }

        public static T DeserializeOB<T>(byte[] data) where T : MyObjectBuilder_Base
        {
            if (data == null)
                return null;

            using (var m = new MemoryStream(data))
            {
                MyObjectBuilderSerializer.DeserializePB(m, out T obj);
                return obj;
            }
        }
    }
}
