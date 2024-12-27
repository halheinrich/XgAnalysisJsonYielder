﻿namespace XgAnalysisJsonNamespace
{
    public class MatchAnalysis
    {
        public int MatchLength { get; set; }
        public string BottomPlayerName { get; set; } = string.Empty;
        public string TopPlayerName { get; set; } = string.Empty;
        public string MatchFileName { get; set; } = string.Empty;
        public List<GameAnalysis> GameAnalysisList { get; set; } = [];
    }
    public class GameAnalysis
    {
        public int GameNumber { get; set; }
        public int BottomPlayerNeeds { get; set; }
        public int TopPlayerNeeds { get; set; }
        public bool IsCrawford { get; set; }
        public List<CubeAnalysis> CubeAnalysisList { get; set; } = [];
        public List<CheckerPlayAnalysis> CheckerPlayAnalysisList { get; set; } = [];
    }
    public class CubeAnalysis
    {
        public string XgidTxt { get; set; } = string.Empty;
        public int MoveNumber { get; set; }
        public float NoDoubleEquity { get; set; }
        public float DoubleTakeEquity { get; set; }
        public float WrongPassThreshold { get; set; }
        public string AnalysisDepth { get; set; } = string.Empty;
    }
    public class CheckerPlayAnalysis
    {
        public string XgidTxt { get; set; } = string.Empty;
        public int MoveNumber { get; set; }
        public List<CheckerPlayVariationAnalysis> VariationList { get; set; } = [];
    }
    public class CheckerPlayVariationAnalysis
    {
        public string MoveTxt { get; set; } = string.Empty;
        public string AnalysisDepth { get; set; } = string.Empty;
        public float CheckerPlayEquity { get; set; }
    }
}