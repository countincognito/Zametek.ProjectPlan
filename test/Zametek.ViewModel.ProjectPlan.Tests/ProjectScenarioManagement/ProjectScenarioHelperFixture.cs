using Newtonsoft.Json.Linq;
using System;
using System.IO;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class ProjectScenarioHelperFixture
        : IDisposable
    {
        public static readonly TheoryData<int[], (int, int)[], (int, int)[]> IdMappingData = new()
        {
            {
                [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                [],
                [(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (6, 6), (7, 7), (8, 8), (9, 9), (10, 10)]
            },
            {
                [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                [(3, 4), (4, 9), (5, 8)],
                [(1, 1), (2, 2), (3, 4), (4, 9), (5, 8), (6, 6), (7, 7), (8, 10), (9, 11), (10, 12)]
            },
            {
                [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                [(8, 4), (9, 5), (10, 6)],
                [(1, 1), (2, 2), (3, 3), (4, 7), (5, 8), (6, 9), (7, 10), (8, 4), (9, 5), (10, 6)]
            },
            {
                [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                [(7, 9), (9, 4), (10, 12)],
                [(1, 1), (2, 2), (3, 3), (4, 5), (5, 6), (6, 7), (7, 9), (8, 8), (9, 4), (10, 12)]
            },
            {
                [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
                [(7, 13), (9, 12), (10, 11)],
                [(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (6, 6), (7, 13), (8, 8), (9, 12), (10, 11) ]
            },
            {
                [4, 5, 6, 7, 8, 9, 10, 11, 12, 13],
                [(11, 1), (12, 2), (13, 3)],
                [(4, 4), (5, 5), (6, 6), (7, 7), (8, 8), (9, 9), (10, 10), (11, 1), (12, 2), (13, 3)]
            },
        };

        public ProjectScenarioHelperFixture()
        {
            Input_JsonString = ReadJsonFile(Path.Combine("TestFiles", "input.json"));
            RemappedActivities_JsonString = ReadJsonFile(Path.Combine("TestFiles", "remapped_activities.json"));
            RemappedResources_JsonString = ReadJsonFile(Path.Combine("TestFiles", "remapped_resources.json"));
            RemappedWorkStreams_JsonString = ReadJsonFile(Path.Combine("TestFiles", "remapped_workstreams.json"));

            static string ReadJsonFile(string filename)
            {
                using StreamReader reader = File.OpenText(filename);
                string content = reader.ReadToEnd();
                JObject json = JObject.Parse(content);
                return json.ToString();
            }
        }

        public string Input_JsonString { get; init; }
        public string RemappedActivities_JsonString { get; init; }
        public string RemappedResources_JsonString { get; init; }
        public string RemappedWorkStreams_JsonString { get; init; }

        public void Dispose()
        {
        }
    }
}
