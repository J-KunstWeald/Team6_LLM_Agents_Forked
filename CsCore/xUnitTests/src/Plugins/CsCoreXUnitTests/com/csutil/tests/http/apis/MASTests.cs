using System.Threading.Tasks;
using com.csutil.http.apis;
using Xunit;

namespace com.csutil.integrationTests.http
{
    public class MASTests {
        private class DebuggingAgent : LLMAgent {
            public override void FeedUserMessage(string message) {
                Log.d("message: " + message);
                Assert.True(System.Diagnostics.Debugger.IsAttached);
                System.Diagnostics.Debugger.Break();
            }

            public override async Task<T> GenerateResponse<T>(params T[] responseExamples) {
                Assert.True(System.Diagnostics.Debugger.IsAttached);
                Assert.NotEmpty(responseExamples);
                // fill this value
                T response = responseExamples[0].DeepCopy();
                System.Diagnostics.Debugger.Break();
                return response;
            }

            public override void ForgetConversation() { }
        }
        
        public MASTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        [Fact]
        public async Task TestOne() {
            var api = new OpenAi(await IoC.inject.GetAppSecrets().GetSecret("OpenAiKey"));
            LLMAgent delilah = new LLMAgent.ChatGpt(api);
            LLMAgent samson = new LLMAgent.ChatGpt(api);

            double x;

            {
                delilah.FeedUserMessage("Solve the following equation for x: 10 * x = 20");
                var response = await delilah.GenerateNumericResponse();
                Log.d("promptSummary: " + response.promptSummary);
                Log.d("response: " + response.number);
                x = response.number;
            }
            
            Assert.Equal(2.0, x);
            
            bool judgement;
            int confidence;

            {
                samson.FeedUserMessage("Is 10 * " + x + " = 20?");
                var response = await samson.GenerateYesNoResponse();
                Log.d("promptSummary: " + response.promptSummary);
                Log.d("judgment: " + response.answer);
                judgement = response.answer;
                Log.d("confidence: " + response.confidenceInAnswer);
                confidence = response.confidenceInAnswer;
                Log.d("explanation: " + response.explanation);
            }
            
            Assert.True(judgement);
            Assert.True(confidence == 100);
        }

        [Fact]
        public async Task TestTwo() {
            var api = new OpenAi(await IoC.inject.GetAppSecrets().GetSecret("OpenAiKey"));
            LLMAgent chooser = new DebuggingAgent();
            LLMAgent guesser = new LLMAgent.ChatGpt(api, "gpt-4-0125-preview",
                "You are the player of a game of guessing a famous person by posing seven true/false"
                + "questions in a JSON format.");
            
            chooser.FeedUserMessage("Choose a person, at least as famous as Sandro Botticelli");
            var choice = (await chooser.GenerateTextResponse()).response;
            
            Log.d("choice: " + choice);

            var questionExample1 = new LLMAgentExtensions.TextResponse() {
                promptSummary = "My true/false question for guessing the person",
                response = "Is the person still alive?"
            };

            var questionExample2 = new LLMAgentExtensions.TextResponse() {
                promptSummary = "My true/false question for guessing the person",
                response = "Is the person an artist?"
            };

            for (int i = 1; i <= 7; ++i) {
                guesser.FeedUserMessage("What is your " + i + ". question?");
                var question = (await guesser.GenerateTextResponse(questionExample1, questionExample2)).response;
                Log.d("question #" + i + ": " + question);

                chooser.ForgetConversation();
                
                chooser.FeedUserMessage("Answer the following question about your chosen person \"" + choice
                    + "\" truthfully: " + question);

                var answer = (await chooser.GenerateYesNoResponse()).answer;
                
                Log.d("answer #" + i + ": " + answer);
                
                guesser.FeedUserMessage("The answer to the previous question is: " + answer);
            }
            
            guesser.FeedUserMessage("Now guess the character, respond with just their name.");
            var guess = (await guesser.GenerateTextResponse()).response;
            
            Log.d("guess: " + guess);
            
            chooser.FeedUserMessage("Your chosen person was \"" + choice + "\", the other players guess was"
                                  + "\"" + guess + "\", is that the correct answer?");

            bool answerCorrect = (await chooser.GenerateYesNoResponse()).answer;
            
            Assert.True(answerCorrect);
            Assert.Equal(choice, guess);
        }

        [Fact]
        public async Task TestThree() {
            var api = new OpenAi(await IoC.inject.GetAppSecrets().GetSecret("OpenAiKey"));
            LLMAgent chooser = new LLMAgent.ChatGpt(api, "gpt-4-0125-preview", 
                "You are the chooser in a game of guessing a famous person, you partner"
                + "will ask you a series of true/false questions, answer them truthfully in a JSON format as"
                + "requested.");
            LLMAgent guesser = new LLMAgent.ChatGpt(api, "gpt-4-0125-preview",
                "You are the player of a game of guessing a famous person by posing seven true/false"
                + "questions in a JSON format.");
            
            chooser.FeedUserMessage("Choose a person, at least as famous as Sandro Botticelli");
            var choice = (await chooser.GenerateTextResponse()).response;
            
            Log.d("choice: " + choice);

            var questionExample1 = new LLMAgentExtensions.TextResponse() {
                promptSummary = "My true/false question for guessing the person",
                response = "Is the person still alive?"
            };

            var questionExample2 = new LLMAgentExtensions.TextResponse() {
                promptSummary = "My true/false question for guessing the person",
                response = "Is the person an artist?"
            };

            for (int i = 1; i <= 7; ++i) {
                guesser.FeedUserMessage("What is your " + i + ". question?");
                var question = (await guesser.GenerateTextResponse(questionExample1, questionExample2)).response;
                Log.d("question #" + i + ": " + question);

                chooser.ForgetConversation();
                
                chooser.FeedUserMessage("Answer the following question about your chosen person \"" + choice
                    + "\" truthfully: " + question);

                var answer = (await chooser.GenerateYesNoResponse()).answer;
                
                Log.d("answer #" + i + ": " + answer);
                
                guesser.FeedUserMessage("The answer to the previous question is: " + answer);
            }
            
            guesser.FeedUserMessage("Now guess the character, respond with just their name.");
            var guess = (await guesser.GenerateTextResponse()).response;
            
            Log.d("guess: " + guess);
            
            chooser.FeedUserMessage("Your chosen person was \"" + choice + "\", the other players guess was"
                                  + "\"" + guess + "\", is that the correct answer?");

            bool answerCorrect = (await chooser.GenerateYesNoResponse()).answer;
            
            Assert.True(answerCorrect);
            Assert.Equal(choice, guess);
        }
    }
}