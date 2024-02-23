using System.Threading.Tasks;
using com.csutil.http.apis;
using Xunit;

namespace com.csutil.integrationTests.http
{
    public class MASTests {
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
    }
}