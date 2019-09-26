using System;
using System.Collections.Generic;

namespace CodeChallenge.Models
{
    public class StackOverflowSearchVM
    {
        public List<StackOverflowResultVM> items { get; set; }
    }

    public class StackOverflowResultVM
    {

        public int question_id { get; set; }
        public long creation_date { get; set; } //Dates in Unix Epoch time
        public string title { get; set; }
    }

    public class StackOverflowDetailsVM
    {
        public List<StackOverflowQuestionVM> items { get; set; }
    }

    public class StackOverflowQuestionVM
    {
        public int question_id { get; set; }
        public int accepted_answer_id { get; set; }
        public List<StackOverflowAnswerVM> answers { get; set; }
        public string body { get; set; }
        public long creation_date { get; set; } //Dates in Unix Epoch time
    }

    public class StackOverflowAnswerVM
    {
        public int answer_id { get; set; }
        public string body { get; set; }
        public long creation_date { get; set; } //dates in Unix Epoch time
        public bool? selected_answer { get; set; }
        public bool? correct_answer { get; set; }
        public int guess_count { get; set; }
        public decimal guess_percentage { get; set; }
    }
}