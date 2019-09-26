using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace CodeChallenge.Helpers
{
    public class GuessResult
    {
        public List<AnswerData> Answers { get; set; }
    }

    public class AnswerData
    {
        public int AnswerID { get; set; }
        public int GuessCount { get; set; }
        public decimal GuessPercentage { get; set; }
    }

    public class GuessHandler
    {
        public GuessResult SubmitGuess(int answerID, int questionID, string description, long questionDate)
        {
            GuessResult result = new GuessResult();

            try
            {
                using (CodingChallengeEntities data = new CodingChallengeEntities())
                {
                    var guessLog = data.GuessLogs.FirstOrDefault(g => g.AnswerID == answerID);
                    if (guessLog == null)
                    {
                        guessLog = new GuessLog()
                        {
                            QuestionID = questionID,
                            AnswerID = answerID,
                            QuestionText = description,
                            QuestionDate = questionDate,
                            GuessCount = 1
                        };

                        data.GuessLogs.Add(guessLog);
                    }
                    else
                    {
                        guessLog.GuessCount++;
                        data.Entry(guessLog).State = System.Data.Entity.EntityState.Modified;
                    }

                    data.SaveChanges();

                    var totalGuesses = data.GuessLogs.Where(g => g.QuestionID == questionID).Sum(g => g.GuessCount);
                    result.Answers = data.GuessLogs.Where(g => g.QuestionID == questionID)
                        .Select(g => new AnswerData()
                        {
                            AnswerID = g.AnswerID,
                            GuessCount = g.GuessCount,
                            GuessPercentage = ((decimal)g.GuessCount / totalGuesses) * 100
                        }).ToList();
                }
            }catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }


            return result;
        }
    }
}
