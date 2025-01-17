using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using com.csutil.http.apis;

namespace com.csutil.http.apis {
    public abstract class LLMAgent {
        public abstract void FeedUserMessage(string message);

        public abstract Task<T> GenerateResponse<T>(params T[] responseExamples);

        public abstract void ForgetConversation();
        
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

            public override async Task<T> GenerateResponse<T>(params T[] responseExamples) {
                messages.Add(new apis.ChatGpt.Line(apis.ChatGpt.Role.system, content: apis.ChatGptExtensions.CreateJsonInstructions(responseExamples)));
                
                var request = new apis.ChatGpt.Request(messages);
                
                request.response_format = apis.ChatGpt.Request.ResponseFormat.json;
                request.model = model;
                
                var response = await api.ChatGpt(request);
                var line = response.choices.Single().message;
                
                // removing the json instructions is necessary to not blow up the context window
                messages.RemoveAt(messages.Count - 1);
                messages.Add(line);
                return JsonReader.GetReader().Read<T>(line.content);
            }

            public override void ForgetConversation() {
                messages.RemoveRange(1, messages.Count - 1);
            }
        }
    }

    public static class LLMAgentExtensions {
        public class TextResponse {
            public string promptSummary { get; set; }
            public string response { get; set; }
        }

        public static Task<TextResponse> GenerateTextResponse(this LLMAgent agent, params TextResponse[] exampleResponses) {
            if (exampleResponses.IsEmpty()) {
                var exampleResponse = new TextResponse() {
                    promptSummary = "Am I sentient?",
                    response = "I am not."
                };

                return agent.GenerateResponse(exampleResponse);
            } else {
                return agent.GenerateResponse(exampleResponses);
            }
        }

        public class YesNoResponse {
            public string promptSummary { get; set; }
            public string explanation { get; set; }
            public int confidenceInAnswer { get; set; }
            public bool answer { get; set; }
        }

        public static Task<YesNoResponse> GenerateYesNoResponse(this LLMAgent agent, params YesNoResponse[] exampleResponses) {
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

                return agent.GenerateResponse(positiveExample, negativeExample);
            } else {
                return agent.GenerateResponse(exampleResponses);
            }
        }
        
        public class NumericResponse {
            public string promptSummary { get; set; }
            public double number { get; set; }
        }

        public static Task<NumericResponse> GenerateNumericResponse(this LLMAgent agent, params NumericResponse[] exampleResponses) {
            if (exampleResponses.IsEmpty()) {
                var exampleResponse = new NumericResponse() {
                    promptSummary = "2*2",
                    number = 4.0
                };

                return agent.GenerateResponse(exampleResponse);
            } else {
                return agent.GenerateResponse(exampleResponses);
            }
        }
    }
}