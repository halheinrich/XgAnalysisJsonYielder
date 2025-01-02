using System.Diagnostics;
using XgAnalysisJsonNamespace;

namespace XgAnalysisJsonYielderNamespace
{
    public class XgAnalysisJsonYielder_xUnit
    {
        [Fact]
        public void TestToBeUploaded()
        {
            const string xgHtmlAnalysisDirectory = @"C:\Users\halhe\Documents\eXtremeGammon\Exports Web\toBeUploaded";
            const string jsonOutputDirectory = @"C:\Users\halhe\Documents\eXtremeGammon\Exports Web\JSON";
            Stopwatch stopwatch = Stopwatch.StartNew();
            int matchCount = JsonFromXgAnalysisHtml(xgHtmlAnalysisDirectory, jsonOutputDirectory);
            stopwatch.Stop();
            Debug.Assert(matchCount > 0);
            double elapsedMinutes = stopwatch.Elapsed.TotalMinutes;
            Debug.WriteLine($"TestToBeUploaded completed {matchCount} matches in {elapsedMinutes:F2} minutes");
        }

        [Fact]
        public void TestUploadComplete()
        {
            const string xgHtmlAnalysisDirectory = @"C:\Users\halhe\Documents\eXtremeGammon\Exports Web\uploadComplete";
            const string jsonOutputDirectory = @"C:\Users\halhe\Documents\eXtremeGammon\Exports Web\JSON";
            int maxMatches = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            int matchCount = JsonFromXgAnalysisHtml(xgHtmlAnalysisDirectory, jsonOutputDirectory, maxMatches);
            stopwatch.Stop();
            Debug.Assert(matchCount >= (maxMatches == 0 ? 17892 : maxMatches)); 
            double elapsedMinutes = stopwatch.Elapsed.TotalMinutes;
            Debug.WriteLine($"TestUploadComplete completed {matchCount} matches in {elapsedMinutes:F2} minutes");
        }
        public static int JsonFromXgAnalysisHtml(string _XgHtmlAnalysisDirectory, string _JsonOutputDirectory, int _MaxMatchCt = 0)
        {
            int matchCount = 0;
            foreach (MatchAnalysis xgMatchAnalysis in XgAnalysisJsonYielder.XgAnalysisMatchJsonYielder(_XgHtmlAnalysisDirectory, _JsonOutputDirectory))
            {
                ++matchCount;
                xgMatchAnalysis.WriteToJsonFile(_JsonOutputDirectory);
                MatchAnalysis matchAnalysisFromFile = new MatchAnalysis($@"C:\Users\halhe\Documents\eXtremeGammon\Exports Web\JSON\{xgMatchAnalysis.MatchFileName}.json");
                Debug.Assert(xgMatchAnalysis.Equals(matchAnalysisFromFile));
                if (_MaxMatchCt > 0 && matchCount >= _MaxMatchCt)
                    break;
            }
            return matchCount;
        }
    }
}
