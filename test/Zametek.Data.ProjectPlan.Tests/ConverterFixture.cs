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
            V0_1_0_JsonString = ReadJsonFile(@"TestFiles\test_v0_1_0.zpp");
            V0_2_0_JsonString = ReadJsonFile(@"TestFiles\test_v0_2_0.zpp");
            V0_2_1_JsonString = ReadJsonFile(@"TestFiles\test_v0_2_1.zpp");
            V0_3_0_JsonString = ReadJsonFile(@"TestFiles\test_v0_3_0.zpp");
            V0_3_1_JsonString = ReadJsonFile(@"TestFiles\test_v0_3_1.zpp");
            V0_3_2_JsonString = ReadJsonFile(@"TestFiles\test_v0_3_2.zpp");
            Va_0_3_2_JsonString = ReadJsonFile(@"TestFiles\test-a_v0_3_2.zpp");
            Va_0_4_0_JsonString = ReadJsonFile(@"TestFiles\test-a_v0_4_0.zpp");

            static string ReadJsonFile(string filename)
            {
                using StreamReader reader = File.OpenText(filename);
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
        public string Va_0_3_2_JsonString { get; init; }
        public string Va_0_4_0_JsonString { get; init; }

        public void Dispose()
        {
        }
    }
}
