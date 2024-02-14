using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using com.csutil.http.apis;

namespace com.csutil.http.apis {
    public abstract class LLMAgent {
        public abstract void FeedUserMessage(string message);

        public class TextResponse {
            public string promptSummary { get; set; }
            public string response { get; set; }
        }

        public Task<TextResponse> GenerateTextResponse(params TextResponse[] exampleResponses) {
            if (exampleResponses.IsEmpty()) {
                var exampleResponse = new TextResponse() {
                    promptSummary = "Am I sentient?",
                    response = "I am not."
                };

                return GenerateResponse(exampleResponse);
            } else {
                return GenerateResponse(exampleResponses);
            }
        }

        public class YesNoResponse {
            public string promptSummary { get; set; }
            public string explanation { get; set; }
            public int confidenceInAnswer { get; set; }
            public bool answer { get; set; }
        }

        public Task<YesNoResponse> GenerateYesNoResponse(params YesNoResponse[] exampleResponses) {
            if (exampleResponses.IsEmpty()) {
                var positiveExample = new YesNoResponse() {
                    promptSummary = "Are humans a type of animal?",
                    explanation = "Humans belong to the animal species Homo Sapiens, however in colloquial language the term animal is often used to designate nonhuman animals.",
                    confidenceInAnswer = 90,
                    answer = true
                };
                var negativeExample = new YesNoResponse() {
                    promptSummary = "Are plants a type of animal?",
                    explanation = "Animals and plants both belong to the domain of eukaryotes, however they constitute two different kingdoms, namely Animalia and Plantae, within it.",
                    confidenceInAnswer = 100,
                    answer = false
                };

                return GenerateResponse(positiveExample, negativeExample);
            } else {
                return GenerateResponse(exampleResponses);
            }
        }
        
        public class NumericResponse {
            public string promptSummary { get; set; }
            public double number { get; set; }
        }

        public Task<NumericResponse> GenerateNumericResponse(params NumericResponse[] exampleResponses) {
            if (exampleResponses.IsEmpty()) {
                var exampleResponse = new NumericResponse() {
                    promptSummary = "2*2",
                    number = 4.0
                };

                return GenerateResponse(exampleResponse);
            } else {
                return GenerateResponse(exampleResponses);
            }
        }
        
        protected abstract Task<T> GenerateResponse<T>(params T[] responseExamples);

        public class ChatGpt : LLMAgent {
            private OpenAi api;
            private string model;
            private List<apis.ChatGpt.Line> messages;
            
            public ChatGpt(OpenAi api, string model = "gpt-3.5-turbo-1106", string systemInstructions = "You are a helpful assistant designed to output JSON.") {
                this.api = api;
                this.model = model;
                messages = new List<apis.ChatGpt.Line>();
                messages.Add(new apis.ChatGpt.Line(apis.ChatGpt.Role.system, content: systemInstructions));
            }

            public override void FeedUserMessage(string message) {
                messages.Add(new apis.ChatGpt.Line(apis.ChatGpt.Role.user, content: message));
            }

            protected override async Task<T> GenerateResponse<T>(params T[] responseExamples) {
                messages.Add(new apis.ChatGpt.Line(apis.ChatGpt.Role.system, content: apis.ChatGptExtensions.CreateJsonInstructions(responseExamples)));
                
                var request = new apis.ChatGpt.Request(messages);
                
                request.response_format = apis.ChatGpt.Request.ResponseFormat.json;
                request.model = model;
                
                var response = await api.ChatGpt(request);
                var line = response.choices.Single().message;
                
                messages.Add(line);
                return JsonReader.GetReader().Read<T>(line.content);
            }
        }
    }
}