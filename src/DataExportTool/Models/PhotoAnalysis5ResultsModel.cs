using System;

namespace ChangiDataExport.Models
{
    public class PhotoAnalysis5ResultsModel : IPhotoAnalysisResultModel
    {
        public string SearchImage { get; set; }
        public string Result1Image { get; set; }
        public string Result1MemberId { get; set; }
        public int? Result1Score { get; set; }
        public string Result2Image { get; set; }
        public string Result2MemberId { get; set; }
        public int? Result2Score { get; set; }
        public string Result3Image { get; set; }
        public string Result3MemberId { get; set; }
        public int? Result3Score { get; set; }
        public string Result4Image { get; set; }
        public string Result4MemberId { get; set; }
        public int? Result4Score { get; set; }
        public string Result5Image { get; set; }
        public string Result5MemberId { get; set; }
        public int? Result5Score { get; set; }
        public DateTime SearchStart { get; set; }
        public DateTime SearchEnd { get; set; }
    }
}