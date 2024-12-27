using System.Diagnostics;
using XgAnalysisJsonNamespace;

namespace XgAnalysisJsonYielderNamespace
{
    public class XgAnalysisJsonYielder
    {
        #region Main Loop
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
                    return ProcessMatchDirectory(matchDirectory);
                });

            foreach (MatchAnalysis matchAnalysis in matchAnalysisTasks)
            {
                yield return matchAnalysis;
            }
        }
        #endregion Main Loop
        #region Match helper methods
        private static IEnumerable<MatchAnalysis> ProcessMatchDirectory(string _MatchDirectory)
        {
            DirectoryInfo? xgAnalysisDirInfo = new(_MatchDirectory);
            yield return ProcessMatchFile(xgAnalysisDirInfo);
        }
        private static MatchAnalysis ProcessMatchFile(DirectoryInfo _XgAnalysisDirInfo)
        {
            FileInfo[]? xgGameHtmArray = _XgAnalysisDirInfo.GetFiles("game0.htm", SearchOption.TopDirectoryOnly);
            if (xgGameHtmArray.Length == 0)
            {
                throw new FileNotFoundException("Missing game0.htm", _XgAnalysisDirInfo.FullName);
            }
            string matchFullName = xgGameHtmArray[0].FullName;
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

            MatchAnalysis xgMatchJson = new()
            {
                MatchFileName = matchFullName.Substring(secondLastSeparatorIndex + 1, lastSeparatorIndex - secondLastSeparatorIndex - 1)
            };

            string xgMatchText = File.ReadAllText(matchFullName);
            int endIndex = ExtractPlayerNames(xgMatchText, xgMatchJson);
            ExtractMatchLength(xgMatchText, xgMatchJson, endIndex);

            LoadXgAnalysisGameJson(xgMatchJson, _XgAnalysisDirInfo);

            return xgMatchJson;
        }
        private static int ExtractPlayerNames(string _XgMatchText, MatchAnalysis _XgMatchJson)
        {
            const string vsToken = " vs. ";
            int vsSeparatorIndex = _XgMatchText.IndexOf(vsToken, StringComparison.Ordinal);
            int htmlEndSeparatorIndex = _XgMatchText.LastIndexOf('>', vsSeparatorIndex);
            _XgMatchJson.BottomPlayerName = _XgMatchText.Substring(htmlEndSeparatorIndex + 1, vsSeparatorIndex - htmlEndSeparatorIndex - 1);

            int htmlBeginSeparatorIndex = _XgMatchText.IndexOf('<', vsSeparatorIndex + vsToken.Length);
            _XgMatchJson.TopPlayerName = _XgMatchText.Substring(vsSeparatorIndex + vsToken.Length, htmlBeginSeparatorIndex - vsSeparatorIndex - vsToken.Length);
            return htmlBeginSeparatorIndex;
        }
        private static void ExtractMatchLength(string _XgMatchText, MatchAnalysis _XgMatchJson, int _StartIndex)
        {
            const string matchLengthToken = " point match, ";
            int matchLengthSeparatorIndex = _XgMatchText.IndexOf(matchLengthToken, _StartIndex, StringComparison.Ordinal);
            if (matchLengthSeparatorIndex == -1)
            {
                const string unlimitedSessionToken = ">Unlimited Game, ";
                if (_XgMatchText.Contains(unlimitedSessionToken))
                    _XgMatchJson.MatchLength = 0;
                else
                    throw new InvalidOperationException($"Invalid match length");
            }
            else
            {
                int htmlEndSeparatorIndex = _XgMatchText.LastIndexOf('>', matchLengthSeparatorIndex);
                string matchLengthString = _XgMatchText.Substring(htmlEndSeparatorIndex + 1, matchLengthSeparatorIndex - htmlEndSeparatorIndex - 1);
                if (int.TryParse(matchLengthString, out int matchLength))
                {
                    _XgMatchJson.MatchLength = matchLength;
                }
                else
                {
                    throw new InvalidOperationException($"Invalid match length value: {matchLengthString}");
                }
            }
        }
        #endregion Match helper methods
        #region Game helper methods
        public static void LoadXgAnalysisGameJson(MatchAnalysis _XgMatchAnalysis, DirectoryInfo _XgAnalysisDirInfo)
        {
            FileInfo[]? xgGameHtmArray = _XgAnalysisDirInfo.GetFiles("game*.htm", SearchOption.TopDirectoryOnly);
            if (xgGameHtmArray.Length == 0)
            {
                throw new FileNotFoundException("Missing game*.htm", _XgAnalysisDirInfo.FullName);
            }
            int gameCt = xgGameHtmArray.Count() - 2;
            FileInfo[] xgGameHtmSorted = new FileInfo[gameCt];
            foreach (FileInfo xgGameHtm in xgGameHtmArray)
            {
                if (xgGameHtm.Name == "game0.htm" || xgGameHtm.Name == "gamelist.htm")
                    continue;
                string gameNumberString = xgGameHtm.Name.Substring(4, xgGameHtm.Name.Length - 8);
                if (int.TryParse(gameNumberString, out int gameNumber))
                    xgGameHtmSorted[gameNumber - 1] = xgGameHtm;
                else
                    throw new InvalidOperationException($"Invalid game number in file name: {xgGameHtm.Name}");
                xgGameHtmSorted[gameNumber - 1] = xgGameHtm;
            }
            for (int i = 0; i < xgGameHtmSorted.Length; i++)
            {
                GameAnalysis xgGameAnalysis = new();
                string xgGameText = File.ReadAllText(xgGameHtmSorted[i].FullName);
                xgGameAnalysis.GameNumber = i + 1;
                int endIndex = ValidateGameNumber(xgGameText, xgGameAnalysis);
                endIndex = ValidateMatchLength(xgGameText, _XgMatchAnalysis, endIndex);
                endIndex = ExtractPlayerNeeds(xgGameText, _XgMatchAnalysis, xgGameAnalysis, endIndex);
                endIndex = ExtractCrawford(xgGameText, xgGameAnalysis, endIndex);
                _XgMatchAnalysis.GameAnalysisList.Add(xgGameAnalysis);
            }
        }
        private static int ValidateGameNumber(string _XgMatchText, GameAnalysis _XgGameJson)
        {
            const string gameNumberStartToken = "<title>Game ";
            const string gameNumberEndToken = "</title>";
            int gameNumberStartIndex = _XgMatchText.IndexOf(gameNumberStartToken, StringComparison.Ordinal) + gameNumberStartToken.Length;
            int gameNumberEndIndex = _XgMatchText.IndexOf(gameNumberEndToken, gameNumberStartIndex, StringComparison.Ordinal);
            if (gameNumberStartIndex == -1 || gameNumberEndIndex == -1)
            {
                throw new InvalidOperationException($"Invalid game number");
            }
            string gameNumberString = _XgMatchText.Substring(gameNumberStartIndex, gameNumberEndIndex - gameNumberStartIndex);
            if (int.TryParse(gameNumberString, out int gameNumber))
                Debug.Assert(gameNumber == _XgGameJson.GameNumber, "ValidateGameNumber error");
            else
                throw new InvalidOperationException($"Invalid game number");

            return gameNumberEndIndex + gameNumberEndToken.Length;
        }
        private static int ValidateMatchLength(string _XgGameText, MatchAnalysis _XgMatchJson, int _StartIndex)
        {
            const string matchLengthToken = " point match, ";
            int matchLengthSeparatorIndex = _XgGameText.IndexOf(matchLengthToken, _StartIndex, StringComparison.Ordinal);
            if (matchLengthSeparatorIndex == -1)
            {
                const string unlimitedSessionToken = ">Unlimited Game, ";
                matchLengthSeparatorIndex = _XgGameText.IndexOf(unlimitedSessionToken, _StartIndex, StringComparison.Ordinal);
                if (matchLengthSeparatorIndex == -1)
                    throw new InvalidOperationException($"Invalid match length");
                else
                    Debug.Assert(_XgMatchJson.MatchLength == 0, "ValidateMatchLength error");
            }
            else
            {
                int htmlEndSeparatorIndex = _XgGameText.LastIndexOf('>', matchLengthSeparatorIndex);
                string matchLengthString = _XgGameText.Substring(htmlEndSeparatorIndex + 1, matchLengthSeparatorIndex - htmlEndSeparatorIndex - 1);
                if (int.TryParse(matchLengthString, out int matchLength))
                    Debug.Assert(_XgMatchJson.MatchLength == matchLength, "ValidateMatchLength error");
                else
                    throw new InvalidOperationException($"Invalid match length value: {matchLengthString}");
            }
            return matchLengthSeparatorIndex + matchLengthToken.Length;
        }
        private static int ExtractPlayerNeeds(string _XgGameText, MatchAnalysis _XgMatchJson, GameAnalysis _XgGameJson, int _StartIndex)
        {
            const string scoreStartToken = ", Score is";
            string bottomPlayerStartToken = "/> " + _XgMatchJson.BottomPlayerName + ": ";
            const char bottomPlayerEndChar = ',';
            int scoreStartIndex = _XgGameText.IndexOf(scoreStartToken, _StartIndex, StringComparison.Ordinal) + scoreStartToken.Length;

            // Bottom player needs
            int bottomPlayerStartIndex = _XgGameText.IndexOf(bottomPlayerStartToken, scoreStartIndex, StringComparison.Ordinal) + bottomPlayerStartToken.Length;
            int bottomPlayerEndIndex = _XgGameText.IndexOf(bottomPlayerEndChar, bottomPlayerStartIndex) + bottomPlayerStartToken.Length;
            string bottomPlayerScoreText = _XgGameText.Substring(bottomPlayerStartIndex, bottomPlayerEndIndex - bottomPlayerStartIndex - bottomPlayerStartToken.Length);
            if (!int.TryParse(bottomPlayerScoreText, out int bottomPlayerScore))
                throw new InvalidOperationException($"Invalid bottom player score: {bottomPlayerScoreText}");
            _XgGameJson.BottomPlayerNeeds = _XgMatchJson.MatchLength == 0 ? bottomPlayerScore : _XgMatchJson.MatchLength - bottomPlayerScore;

            // Top player needs
            string topPlayerStartToken = "/> " + _XgMatchJson.TopPlayerName + ": ";
            const char topPlayerEndChar = ' ';
            int topPlayerStartIndex = _XgGameText.IndexOf(topPlayerStartToken, bottomPlayerEndIndex, StringComparison.Ordinal) + topPlayerStartToken.Length;
            int topPlayerEndIndex = _XgGameText.IndexOf(topPlayerEndChar, topPlayerStartIndex);
            string topPlayerScoreText = _XgGameText.Substring(topPlayerStartIndex, topPlayerEndIndex - topPlayerStartIndex);
            if (!int.TryParse(topPlayerScoreText, out int topPlayerScore))
                throw new InvalidOperationException($"Invalid top player score: {topPlayerScoreText}");
            _XgGameJson.TopPlayerNeeds = _XgMatchJson.MatchLength == 0 ? topPlayerScore : _XgMatchJson.MatchLength - topPlayerScore;
            return topPlayerEndIndex;
        }
        private static int ExtractCrawford(string _XgGameText, GameAnalysis _XgGameJson, int _StartIndex)
        {
            const string crawfordToken = " Crawford<";
            int crawfordIndex = _XgGameText.IndexOf(crawfordToken, _StartIndex, StringComparison.Ordinal);
            _XgGameJson.IsCrawford = crawfordIndex != -1;
            return crawfordIndex == -1 ? _StartIndex : crawfordIndex + crawfordToken.Length;
        }
        #endregion Game helper methods
    }
}

