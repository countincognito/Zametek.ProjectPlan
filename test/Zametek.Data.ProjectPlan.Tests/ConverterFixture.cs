using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Zametek.Data.ProjectPlan.Tests
{
    public class ConverterFixture
        : IDisposable
    {
        public ConverterFixture()
        {
            V0_1_0_JsonString = ReadJsonFile(@"test_v0_1_0.zpp");
            V0_2_0_JsonString = ReadJsonFile(@"test_v0_2_0.zpp");
            V0_2_1_JsonString = ReadJsonFile(@"test_v0_2_1.zpp");
            V0_3_0_JsonString = ReadJsonFile(@"test_v0_3_0.zpp");
            V0_3_1_JsonString = ReadJsonFile(@"test_v0_3_1.zpp");
            V0_3_2_JsonString = ReadJsonFile(@"test_v0_3_2.zpp");
            V0_3_2a_JsonString = ReadJsonFile(@"test_v0_3_2a.zpp");
            V0_4_0a_JsonString = ReadJsonFile(@"test_v0_4_0a.zpp");
            V0_4_0b_JsonString = ReadJsonFile(@"test_v0_4_0b.zpp");
            V0_4_1b_JsonString = ReadJsonFile(@"test_v0_4_1b.zpp");
            V0_4_1c_JsonString = ReadJsonFile(@"test_v0_4_1c.zpp");
            V0_4_2c_JsonString = ReadJsonFile(@"test_v0_4_2c.zpp");
            V0_4_2d_JsonString = ReadJsonFile(@"test_v0_4_2d.zpp");
            V0_4_3d_JsonString = ReadJsonFile(@"test_v0_4_3d.zpp");
            V0_4_4d_JsonString = ReadJsonFile(@"test_v0_4_4d.zpp");
            V0_5_0_JsonString = ReadJsonFile(@"test_v0_5_0.zpp");
            V0_5_0a_JsonString = ReadJsonFile(@"test_v0_5_0a.zpp");
            V0_6_0_JsonString = ReadJsonFile(@"test_v0_6_0.zpp");

            static string ReadJsonFile(string filename)
            {
                string path = Path.Combine(@"TestFiles", filename);
                using StreamReader reader = File.OpenText(path);
                string content = reader.ReadToEnd();
                JObject json = JObject.Parse(content);
                return json.ToString();
            }
        }

        public string V0_1_0_JsonString { get; init; }
        public string V0_2_0_JsonString { get; init; }
        public string V0_2_1_JsonString { get; init; }
        public string V0_3_0_JsonString { get; init; }
        public string V0_3_1_JsonString { get; init; }
        public string V0_3_2_JsonString { get; init; }
        public string V0_3_2a_JsonString { get; init; }
        public string V0_4_0a_JsonString { get; init; }
        public string V0_4_0b_JsonString { get; init; }
        public string V0_4_1b_JsonString { get; init; }
        public string V0_4_1c_JsonString { get; init; }
        public string V0_4_2c_JsonString { get; init; }
        public string V0_4_2d_JsonString { get; init; }
        public string V0_4_3d_JsonString { get; init; }
        public string V0_4_4d_JsonString { get; init; }
        public string V0_5_0_JsonString { get; init; }
        public string V0_5_0a_JsonString { get; init; }
        public string V0_6_0_JsonString { get; init; }

        public void Dispose()
        {
        }
    }
}
