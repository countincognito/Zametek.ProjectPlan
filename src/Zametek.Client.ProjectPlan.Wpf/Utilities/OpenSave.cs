using Newtonsoft.Json;
using System.IO;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public static class OpenSave
    {
        public static T OpenJson<T>(string filename) where T: class, new()
        {
            if (File.Exists(filename))
            {
                using (StreamReader reader = File.OpenText(filename))
                {
                    var jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings());
                    return jsonSerializer.Deserialize(reader, typeof(T)) as T;
                }
            }
            return new T();
        }

        public static void SaveJson<T>(T state, string fileName)
        {
            using (StreamWriter writer = File.CreateText(fileName))
            {
                var jsonSerializer = JsonSerializer.Create(
                    new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Ignore,
                    });
                jsonSerializer.Serialize(writer, state, typeof(T));
            }
        }
    }
}
