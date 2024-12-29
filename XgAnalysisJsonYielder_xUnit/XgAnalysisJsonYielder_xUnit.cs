using System.Diagnostics;
using XgAnalysisJsonNamespace;

namespace XgAnalysisJsonYielderNamespace
{
    public class XgAnalysisJsonYielder_xUnit
    {
        [Fact]
        public void TestToBeUploaded()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int matchCount = JsonFromXgAnalysisHtml(@"C:\Users\halhe\Documents\eXtremeGammon\Exports Web\toBeUploaded");
            stopwatch.Stop();
            Debug.Assert(matchCount > 0);
            double elapsedMinutes = stopwatch.Elapsed.TotalMinutes;
            Debug.WriteLine($"TestToBeUploaded completed {matchCount} matches in {elapsedMinutes:F2} minutes");
        }

        [Fact]
        public void TestUploadComplete()
        {
            int maxMatches = 200;
            Stopwatch stopwatch = Stopwatch.StartNew();
            int matchCount = JsonFromXgAnalysisHtml(@"C:\Users\halhe\Documents\eXtremeGammon\Exports Web\uploadComplete", maxMatches);
            stopwatch.Stop();
            Debug.Assert(matchCount >= (maxMatches == 0 ? 17892 : maxMatches)); 
            double elapsedMinutes = stopwatch.Elapsed.TotalMinutes;
            Debug.WriteLine($"TestUploadComplete completed {matchCount} matches in {elapsedMinutes:F2} minutes");
        }
        public static int JsonFromXgAnalysisHtml(string XgHtmlAnalysisDirectory, int MaxMatchCt = 0)
        {
            int matchCount = 0;
            foreach (MatchAnalysis xgMatchAnalysis in XgAnalysisJsonYielder.XgAnalysisMatchJsonYielder(XgHtmlAnalysisDirectory))
            {
                ++matchCount;
                if (MaxMatchCt > 0 && matchCount >= MaxMatchCt)
                    break;
            }
            return matchCount;
        }
    }
}
