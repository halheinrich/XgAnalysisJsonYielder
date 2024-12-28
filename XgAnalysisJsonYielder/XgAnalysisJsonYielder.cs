using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using XgAnalysisJsonNamespace;
using XgidNamespace;

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
        public static int LoadXgAnalysisGameJson(MatchAnalysis _XgMatchAnalysis, DirectoryInfo _XgAnalysisDirInfo)
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
            int endIndex = -1;
            for (int i = 0; i < xgGameHtmSorted.Length; i++)
            {
                GameAnalysis xgGameAnalysis = new();
                string xgGameText = File.ReadAllText(xgGameHtmSorted[i].FullName);
                xgGameAnalysis.GameNumber = i + 1;
                endIndex = ValidateGameNumber(xgGameText, xgGameAnalysis);
                endIndex = ValidateMatchLength(xgGameText, _XgMatchAnalysis, endIndex);
                endIndex = ExtractPlayerNeeds(xgGameText, _XgMatchAnalysis, xgGameAnalysis, endIndex);
                endIndex = ExtractCrawford(xgGameText, xgGameAnalysis, endIndex);
                LoadXgAnalysisDecisionsJson(xgGameText, xgGameAnalysis, endIndex);
                _XgMatchAnalysis.GameAnalysisList.Add(xgGameAnalysis);
            }
            return endIndex;
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
        #region Decision helper methods
        private static void LoadXgAnalysisDecisionsJson(string _XgGameText, GameAnalysis _XgGameJson, int _StartIndex)
        {
            const string xgIdToken = ">XGID=";
            _StartIndex = _XgGameText.IndexOf(xgIdToken, _StartIndex);
            int endIndex = -1;
            while (_StartIndex != -1)
            {
                endIndex = _XgGameText.IndexOf(xgIdToken, _StartIndex + xgIdToken.Length);
                string decisionAnalysisText = endIndex == -1 ? _XgGameText.Substring(_StartIndex) : _XgGameText.Substring(_StartIndex, endIndex - _StartIndex);
                LoadDecisionAnalysis(decisionAnalysisText, _XgGameJson);
                _StartIndex = endIndex;
            }
        }
        private static void LoadDecisionAnalysis(string _XgGameText, GameAnalysis _XgGameJson)
        {
            int checkerPlayMoveNum = _XgGameJson.CheckerPlayAnalysisList.Count > 0 ? _XgGameJson.CheckerPlayAnalysisList[_XgGameJson.CheckerPlayAnalysisList.Count - 1].MoveNumber : 0;
            int cubeMoveNum = _XgGameJson.CubeAnalysisList.Count > 0 ? _XgGameJson.CubeAnalysisList[_XgGameJson.CubeAnalysisList.Count - 1].MoveNumber : 0;
            int moveNumber = (checkerPlayMoveNum > cubeMoveNum ? checkerPlayMoveNum : cubeMoveNum) + 1;
            int xgidTxtEndIndex = _XgGameText.IndexOf('<');
            string xgidTxt = _XgGameText.Substring(1, xgidTxtEndIndex - 1);
            Xgid xgid = new(xgidTxt);
            if (moveNumber == 1)
            {
                const string xgInitialBoard = "XGID=-b----E-C---eE---c-e----B-:";
                if (xgid.XgidText.Substring(0, xgInitialBoard.Length).Equals(xgInitialBoard))
                    _XgGameJson.IsFromBeginning = true;
            }
            if (xgid.IsCubeDecision)
            {
                ExtractCubeAnalysis(_XgGameText, _XgGameJson, xgid, moveNumber);
            }
            else
            {
                ExtractCheckerPlayAnalysis(_XgGameText, _XgGameJson, xgid, moveNumber);
            }
            //_StartIndex = ExtractXgId(_XgGameText, xgCubeAnalysis, _StartIndex, xgIdToken);
            //_StartIndex = ExtractMoveNumber(_XgGameText, xgCubeAnalysis, _StartIndex);
            //_StartIndex = ExtractNoDoubleEquity(_XgGameText, xgCubeAnalysis, _StartIndex);
            //_StartIndex = ExtractDoubleTakeEquity(_XgGameText, xgCubeAnalysis, _StartIndex);
            //_StartIndex = ExtractWrongPassThreshold(_XgGameText, xgCubeAnalysis, _StartIndex);
            //_StartIndex = ExtractAnalysisDepth(_XgGameText, xgCubeAnalysis, _StartIndex);
            //_XgGameJson.CubeAnalysisList.Add(xgCubeAnalysis);
            //const string crawfordToken = " Crawford<";
            //int crawfordIndex = _XgGameText.IndexOf(crawfordToken, _StartIndex, StringComparison.Ordinal);
            //_XgGameJson.IsCrawford = crawfordIndex != -1;
            //return crawfordIndex == -1 ? _StartIndex : crawfordIndex + crawfordToken.Length;
        }
        private static void ExtractCubeAnalysis(string _XgGameText, GameAnalysis _XgGameJson, Xgid _Xgid, int _MoveNumber)
        {
            Debug.Assert(_Xgid.XgidText != null);
            CubeAnalysis cubeAnalysis = new();
            cubeAnalysis.MoveNumber = _MoveNumber;
            cubeAnalysis.XgidTxt = _Xgid.XgidText;
            int endIndex = ExtractAnalysisDepth(_XgGameText, cubeAnalysis, cubeAnalysis.XgidTxt.Length);
            endIndex = ExtractNoDoubleEquity(_XgGameText, cubeAnalysis, cubeAnalysis.XgidTxt.Length);
            endIndex = ExtractDoubleTakeEquity(_XgGameText, cubeAnalysis, cubeAnalysis.XgidTxt.Length);
            endIndex = ExtractWrongPassThreshold(_XgGameText, cubeAnalysis, cubeAnalysis.XgidTxt.Length);
            _XgGameJson.CubeAnalysisList.Add(cubeAnalysis);
        }
        private static void ExtractCheckerPlayAnalysis(string _XgGameText, GameAnalysis _XgGameJson, Xgid _Xgid, int _MoveNumber)
        {
            Debug.Assert(_Xgid.XgidText != null);
            CheckerPlayAnalysis checkerPlayAnalysis = new();
            checkerPlayAnalysis.MoveNumber = _MoveNumber;
            checkerPlayAnalysis.XgidTxt = _Xgid.XgidText;
            _XgGameJson.CheckerPlayAnalysisList.Add(checkerPlayAnalysis);
        }
        private static int ExtractAnalysisDepth(string _XgGameText, CubeAnalysis _CubeAnalysis, int _StartIndex)
        {
            const string analysisDepthStartToken = ">Analyzed in ";
            const string analysisDepthEndToken = "</td>";
            int analysisDepthStartIndex = _XgGameText.IndexOf(analysisDepthStartToken, _StartIndex) + analysisDepthStartToken.Length;
            Debug.Assert(analysisDepthStartIndex != -1, "AnalysisDepth error");
            int analysisDepthEndIndex = _XgGameText.IndexOf(analysisDepthEndToken, analysisDepthStartIndex);
            Debug.Assert(analysisDepthEndIndex != -1, "AnalysisDepth error");
            _CubeAnalysis.AnalysisDepth = _XgGameText.Substring(analysisDepthStartIndex, analysisDepthEndIndex - analysisDepthStartIndex);
            if (_CubeAnalysis.AnalysisDepth.Equals("Rollout"))
                Debug.Assert(true);
            return analysisDepthEndIndex + analysisDepthEndToken.Length;
        }
        private static int ExtractNoDoubleEquity(string _XgGameText, CubeAnalysis _CubeAnalysis, int _StartIndex)
        {
            const string noDoubleEquityStartToken = "No double:</td><td>";
            const string noRedoubleEquityStartToken = "No redouble:</td><td>";
            int noDoubleEquityStartIndex = _XgGameText.IndexOf(noDoubleEquityStartToken, _StartIndex);
            if (noDoubleEquityStartIndex == -1)
            {
                noDoubleEquityStartIndex = _XgGameText.IndexOf(noRedoubleEquityStartToken, _StartIndex);
                Debug.Assert(noDoubleEquityStartIndex != -1, "NoDoubleEquity error");
                noDoubleEquityStartIndex += noRedoubleEquityStartToken.Length;
            }
            else
                noDoubleEquityStartIndex += noDoubleEquityStartToken.Length;

            const string noDoubleEquityEndToken1 = "</td>";
            const string noDoubleEquityEndToken2 = " (";
            int noDoubleEquityEndIndex1 = _XgGameText.IndexOf(noDoubleEquityEndToken1, noDoubleEquityStartIndex);
            int noDoubleEquityEndIndex2 = _XgGameText.IndexOf(noDoubleEquityEndToken2, noDoubleEquityStartIndex);
            int noDoubleEquityEndIndex = noDoubleEquityEndIndex1 < noDoubleEquityEndIndex2 ? noDoubleEquityEndIndex1 : noDoubleEquityEndIndex2;
            if (noDoubleEquityEndIndex == -1)
                noDoubleEquityEndIndex = noDoubleEquityEndIndex1 > noDoubleEquityEndIndex2 ? noDoubleEquityEndIndex1 : noDoubleEquityEndIndex2;
            Debug.Assert(noDoubleEquityEndIndex != -1, "NoDoubleEquity error");

            string noDoubleEquityTxt = _XgGameText.Substring(noDoubleEquityStartIndex, noDoubleEquityEndIndex - noDoubleEquityStartIndex);
            if (!float.TryParse(noDoubleEquityTxt, out float noDoubleEquity))
                throw new InvalidOperationException($"Invalid no Double Equity: {noDoubleEquityTxt}");
            _CubeAnalysis.NoDoubleEquity = noDoubleEquity;
            return noDoubleEquityEndIndex1 + noDoubleEquityEndToken1.Length;
        }
        private static int ExtractDoubleTakeEquity(string _XgGameText, CubeAnalysis _CubeAnalysis, int _StartIndex)
        {
            const string doubleTakeEquityStartToken = "Double/Take:</td><td>";
            const string noRedoubleEquityStartToken = "Redouble/Take:</td><td>";
            int doubleTakeEquityStartIndex = _XgGameText.IndexOf(doubleTakeEquityStartToken, _StartIndex);
            if (doubleTakeEquityStartIndex == -1)
            {
                doubleTakeEquityStartIndex = _XgGameText.IndexOf(noRedoubleEquityStartToken, _StartIndex);
                Debug.Assert(doubleTakeEquityStartIndex != -1, "doubleTakeEquity error");
                doubleTakeEquityStartIndex += noRedoubleEquityStartToken.Length;
            }
            else
                doubleTakeEquityStartIndex += doubleTakeEquityStartToken.Length;

            const string doubleTakeEquityEndToken1 = "</td>";
            const string doubleTakeEquityEndToken2 = " (";
            int doubleTakeEquityEndIndex1 = _XgGameText.IndexOf(doubleTakeEquityEndToken1, doubleTakeEquityStartIndex);
            int doubleTakeEquityEndIndex2 = _XgGameText.IndexOf(doubleTakeEquityEndToken2, doubleTakeEquityStartIndex);
            int doubleTakeEquityEndIndex = doubleTakeEquityEndIndex1 < doubleTakeEquityEndIndex2 ? doubleTakeEquityEndIndex1 : doubleTakeEquityEndIndex2;
            if (doubleTakeEquityEndIndex == -1)
                doubleTakeEquityEndIndex = doubleTakeEquityEndIndex1 > doubleTakeEquityEndIndex2 ? doubleTakeEquityEndIndex1 : doubleTakeEquityEndIndex2;
            Debug.Assert(doubleTakeEquityEndIndex != -1, "doubleTakeEquity error");

            string doubleTakeEquityTxt = _XgGameText.Substring(doubleTakeEquityStartIndex, doubleTakeEquityEndIndex - doubleTakeEquityStartIndex);
            if (!float.TryParse(doubleTakeEquityTxt, out float doubleTakeEquity))
                throw new InvalidOperationException($"Invalid Double/Take Equity: {doubleTakeEquityTxt}");
            _CubeAnalysis.DoubleTakeEquity = doubleTakeEquity;
            return doubleTakeEquityEndIndex1 + doubleTakeEquityEndToken1.Length;
        }
        private static int ExtractWrongPassThreshold(string _XgGameText, CubeAnalysis _CubeAnalysis, int _StartIndex)
        {
            const string percentageWrongStartToken = ">Percentage of wrong ";
            int percentageWrongStartIndex = _XgGameText.IndexOf(percentageWrongStartToken, _StartIndex);
            if (percentageWrongStartIndex == -1)
                return _StartIndex;

            const string WrongPassThresholdStartToken = "pass needed to make the double decision right: ";
            const string ThresholdEndToken = "%<";
            int WrongPassThresholdStartIndex = _XgGameText.IndexOf(WrongPassThresholdStartToken, percentageWrongStartIndex);
            if (WrongPassThresholdStartIndex != -1)
            {
                WrongPassThresholdStartIndex += WrongPassThresholdStartToken.Length;
                int WrongPassThresholdEndIndex = _XgGameText.IndexOf(ThresholdEndToken, WrongPassThresholdStartIndex);
                string WrongPassThresholdTxt = _XgGameText.Substring(WrongPassThresholdStartIndex, WrongPassThresholdEndIndex - WrongPassThresholdStartIndex);
                if (!float.TryParse(WrongPassThresholdTxt, out float WrongPassThreshold))
                    throw new InvalidOperationException($"Invalid Double/Take Equity: {WrongPassThresholdTxt}");
                WrongPassThreshold /= (float)100.0;
                _CubeAnalysis.WrongPassThreshold = WrongPassThreshold;
                return WrongPassThresholdEndIndex + 1;
            }

            const string WrongTakeThresholdStartToken = "take needed to make the double decision right: ";
            int WrongTakeThresholdStartIndex = _XgGameText.IndexOf(WrongTakeThresholdStartToken, percentageWrongStartIndex);
            if (WrongTakeThresholdStartIndex != -1)
            {
                WrongTakeThresholdStartIndex += WrongTakeThresholdStartToken.Length;
                int WrongTakeThresholdEndIndex = _XgGameText.IndexOf(ThresholdEndToken, WrongTakeThresholdStartIndex);
                string WrongTakeThresholdTxt = _XgGameText.Substring(WrongTakeThresholdStartIndex, WrongTakeThresholdEndIndex - WrongTakeThresholdStartIndex);
                if (!float.TryParse(WrongTakeThresholdTxt, out float WrongTakeThreshold))
                    throw new InvalidOperationException($"Invalid Double/Take Equity: {WrongTakeThresholdTxt}");
                WrongTakeThreshold /= (float)100.0;
                _CubeAnalysis.WrongTakeThreshold = WrongTakeThreshold;
                return WrongTakeThresholdEndIndex + 1;
            }
            Debug.Assert(false, "WrongTakeThreshold error");
            return -1;
        }
        #endregion Decision helper methods
    }
}

