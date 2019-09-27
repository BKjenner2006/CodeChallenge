using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using CodeChallenge.Models;
using CodeChallenge.Helpers;
using Newtonsoft.Json;

namespace CodeChallenge.Controllers
{
    public class HomeController : Controller
    {
        //Make sure the cookie is established before every request, need this for keeping track of user score
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            string grade = HttpContext.Request.Cookies.Get("grade")?.Value;

            if (String.IsNullOrEmpty(grade))
            {
                HttpCookie cookie = new HttpCookie("grade");
                cookie.Expires = DateTime.UtcNow.AddMonths(1);
                cookie.Path = "/";
                cookie.HttpOnly = false;
                cookie.Value = "N/A";
                Response.Cookies.Add(cookie);

                cookie = new HttpCookie("total");
                cookie.Expires = DateTime.UtcNow.AddMonths(1);
                cookie.Path = "/";
                cookie.HttpOnly = false;
                cookie.Value = "0";
                Response.Cookies.Add(cookie);

                cookie = new HttpCookie("correct");
                cookie.Expires = DateTime.UtcNow.AddMonths(1);
                cookie.Path = "/";
                cookie.HttpOnly = false;
                cookie.Value = "0";
                Response.Cookies.Add(cookie);
            }
        }

        public async Task<ActionResult> Index()
        {
            StackOverflowSearchVM searchResult = await CallStackOverflow<StackOverflowSearchVM>("search", "advanced", "order=desc&sort=creation&accepted=True&answers=2&site=stackoverflow");

            return View(searchResult);
        }

        public ActionResult Guesses()
        {
            GuessHandler handler = new GuessHandler();

            StackOverflowSearchVM searchResult = handler.GetRecentGuesses();

            return View(searchResult);
        }

        public ActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public ActionResult Privacy()
        {
            return View();
        }

        //Submitting the question detail model to this action to maintain the randomized answer sort order
        public async Task<ActionResult> SubmitGuess(StackOverflowQuestionVM vm)
        {
            GuessHandler guessHandler = new GuessHandler();
            var answerID = vm.answers.FirstOrDefault(a => a.selected_answer == true).answer_id;
            GuessResult guessResult = guessHandler.SubmitGuess(answerID, vm.question_id, vm.title, vm.creation_date);

            StackOverflowDetailsVM result = await CallStackOverflow<StackOverflowDetailsVM>("questions", vm.question_id.ToString(), "order=desc&sort=activity&site=stackoverflow&filter=!-*jbN-o8P3E5");
            var question = result.items.FirstOrDefault();
            vm.body = question.body;

            foreach (var answer in vm.answers)
            {
                answer.body = question.answers.FirstOrDefault(a => a.answer_id == answer.answer_id).body;
                answer.creation_date = question.answers.FirstOrDefault(a => a.answer_id == answer.answer_id).creation_date;
                answer.selected_answer = answer.answer_id == answerID;
                answer.correct_answer = answer.answer_id == question.accepted_answer_id;
                answer.guess_count = guessResult.Answers.FirstOrDefault(a => a.AnswerID == answer.answer_id)?.GuessCount ?? 0;
                answer.guess_percentage = guessResult.Answers.FirstOrDefault(a => a.AnswerID == answer.answer_id)?.GuessPercentage ?? 0;
            }

            //Update score
            int total = 0, correct = 0;
#pragma warning disable CS0168 // The variable 'grade' is declared but never used
            string grade;
#pragma warning restore CS0168 // The variable 'grade' is declared but never used
            if (Int32.TryParse(HttpContext.Request.Cookies.Get("total")?.Value, out total))
            {
                total++;
                Response.Cookies["total"].Value = total.ToString();


                if (Int32.TryParse(HttpContext.Request.Cookies.Get("correct")?.Value, out correct))
                {
                    if (vm.answers.Where(a => a.selected_answer.Value && a.correct_answer.Value).Count() > 0)
                    {
                        correct++;
                        Response.Cookies["correct"].Value = correct.ToString();
                    }
                }

                Response.Cookies["grade"].Value = GetLetterGrade(correct, total);
            }

            return PartialView("_QuestionDetails", vm);
        }

        private string GetLetterGrade(int correct, int total)
        {
            string result = "N/A";

            if(total > 0)
            {
                var percent = (decimal)correct / total;
                if (percent >= .9m)
                    result = "A";
                else if (percent >= .8m)
                    result = "B";
                else if (percent >= .7m)
                    result = "C";
                else if (percent >= .6m)
                    result = "D";
                else
                    result = "F";
            }

            return result;
        }

        public async Task<ActionResult> LoadQuestionDetails(int questionID)
        {
            StackOverflowDetailsVM result = await CallStackOverflow<StackOverflowDetailsVM>("questions", questionID.ToString(), "order=desc&sort=activity&site=stackoverflow&filter=!-*jbN-o8P3E5");

            Random rng = new Random();
            //Small data sets, not concerned with time complexity of this sort
            result.items.FirstOrDefault().answers = result.items.FirstOrDefault().answers.OrderBy(a => rng.Next()).ToList();

            return PartialView("_QuestionDetails", result.items.FirstOrDefault());
        }

        //A generic function to call the StackOverflow api, controller is required, function and parameters are optional
        private async Task<T> CallStackOverflow<T>(string controller, string function, string parameters)
        {
            T responseObject = default(T);
            StringBuilder apiUrl = new StringBuilder();
            apiUrl.Append(controller);

            if (!String.IsNullOrEmpty(function))
                apiUrl.Append("/" + function);

            if (!String.IsNullOrEmpty(parameters))
                apiUrl.Append("?" + parameters);


            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://api.stackexchange.com/2.2/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("GZIP"));

                HttpResponseMessage response = await client.GetAsync(apiUrl.ToString());
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        Stream responseStream = await response.Content.ReadAsStreamAsync();
                        using (var decompressed = new MemoryStream())
                        {
                            using (var gs = new GZipStream(responseStream, CompressionMode.Decompress))
                            {
                                gs.CopyTo(decompressed);
                                string json = Encoding.UTF8.GetString(decompressed.ToArray());
                                responseObject = JsonConvert.DeserializeObject<T>(json);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                }
            }

            return responseObject;
        }
    }
}