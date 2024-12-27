using System.Diagnostics;
using System.Linq;
using XgAnalysisJsonNamespace;

namespace XgAnalysisJsonYielderNamespace
{
    public class XgAnalysisJsonYielder
    {
        public static IEnumerable<MatchAnalysis> XgAnalysisMatchJsonYielder(string _XgHtmlAnalysisDir)
        {
            string[] matchDirectories = Directory.GetDirectories(_XgHtmlAnalysisDir);
            if (matchDirectories.Length == 0)
            {
                throw new InvalidOperationException("No directories found in the specified path.");
            }

            var matchAnalysisTasks = matchDirectories
                .AsParallel()
                .Where(matchDirectory => !matchDirectory.EndsWith(@"\images", StringComparison.OrdinalIgnoreCase))
                .SelectMany(matchDirectory =>
                {
                    DirectoryInfo? xgAnalysisDirInfo = new(matchDirectory);
                    FileInfo[]? xgPrintHtmArray = xgAnalysisDirInfo.GetFiles("game0.htm", SearchOption.TopDirectoryOnly);
                    if (xgPrintHtmArray.Length == 0)
                    {
                        throw new FileNotFoundException("Missing game0.htm", matchDirectory);
                    }

                    return xgPrintHtmArray.Select(xgPrintHtm =>
                    {
                        string matchFullName = xgPrintHtm.FullName;
                        int lastSeparatorIndex = matchFullName.LastIndexOf(Path.DirectorySeparatorChar);
                        if (lastSeparatorIndex == -1)
                        {
                            throw new InvalidOperationException($"Path does not contain enough tokens: {matchFullName}");
                        }
                        int secondLastSeparatorIndex = matchFullName.LastIndexOf(Path.DirectorySeparatorChar, lastSeparatorIndex - 1);
                        if (secondLastSeparatorIndex == -1)
                        {
                            throw new InvalidOperationException($"Path does not contain enough tokens: {matchFullName}");
                        }

                        string xgMatchText = File.ReadAllText(matchFullName);
                        MatchAnalysis xgMatchJson = new()
                        {
                            MatchFileName = matchFullName.Substring(secondLastSeparatorIndex + 1, lastSeparatorIndex - secondLastSeparatorIndex - 1)
                        };

                        const string vsToken = " vs. ";
                        int vsSeparatorIndex = xgMatchText.IndexOf(vsToken, StringComparison.Ordinal);
                        int htmlEndSeparatorIndex = xgMatchText.LastIndexOf('>', vsSeparatorIndex);
                        xgMatchJson.BottomPlayerName = xgMatchText.Substring(htmlEndSeparatorIndex + 1, vsSeparatorIndex - htmlEndSeparatorIndex - 1);

                        int htmlBeginSeparatorIndex = xgMatchText.IndexOf('<', vsSeparatorIndex + vsToken.Length);
                        xgMatchJson.TopPlayerName = xgMatchText.Substring(vsSeparatorIndex + vsToken.Length, htmlBeginSeparatorIndex - vsSeparatorIndex - vsToken.Length);

                        const string matchLengthToken = " point match, ";
                        int matchLengthSeparatorIndex = xgMatchText.IndexOf(matchLengthToken, htmlBeginSeparatorIndex, StringComparison.Ordinal);
                        if (matchLengthSeparatorIndex == -1)
                        {
                            const string unlimitedSessionToken = ">Unlimited Game, ";
                            if (xgMatchText.Contains(unlimitedSessionToken))
                            {
                                xgMatchJson.MatchLength = 0;
                            }
                            else
                            {
                                throw new InvalidOperationException($"Invalid match length in file: {matchFullName}");
                            }
                        }
                        else
                        {
                            htmlEndSeparatorIndex = xgMatchText.LastIndexOf('>', matchLengthSeparatorIndex);
                            string matchLengthString = xgMatchText.Substring(htmlEndSeparatorIndex + 1, matchLengthSeparatorIndex - htmlEndSeparatorIndex - 1);
                            if (int.TryParse(matchLengthString, out int matchLength))
                            {
                                xgMatchJson.MatchLength = matchLength;
                            }
                            else
                            {
                                throw new InvalidOperationException($"Invalid match length value: {matchLengthString} in file: {matchFullName}");
                            }
                        }
                        return xgMatchJson;
                    });
                });

            foreach (var matchAnalysis in matchAnalysisTasks)
            {
                yield return matchAnalysis;
            }
        }
    }
}
